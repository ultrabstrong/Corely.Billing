using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Services;

internal class ConsumptionWriter(
    IRepo<ConsumptionEventEntity> consumptionEventRepo,
    IOperationContextAccessor operationContextAccessor,
    IUsageVocabulary usageVocabulary,
    TimeProvider timeProvider,
    ILogger<ConsumptionWriter> logger
) : IConsumptionWriter
{
    private static readonly RetryOptions _retryOptions = new()
    {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromMilliseconds(200),
        MaxDelay = TimeSpan.FromSeconds(3),
        FastFirst = true,
        ShouldRetry = ex => ex is not OperationCanceledException,
    };

    public Task<SaveConsumptionResult> SaveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    ) => WriteAsync(consumptionEvent, finalized: true, ct);

    public Task<SaveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    ) => WriteAsync(consumptionEvent, finalized: false, ct);

    public Task<SettleConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(quantityByGrant);
        return ResolveAsync(accountId, operation, unit, quantityByGrant, ct);
    }

    public Task<SettleConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) => ResolveAsync(accountId, operation, unit, quantityByGrant: null, ct);

    private async Task<SaveConsumptionResult> WriteAsync(
        ConsumptionEvent consumptionEvent,
        bool finalized,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(consumptionEvent);

        if (UnknownUsage(consumptionEvent) is { } unknown)
        {
            logger.LogError(
                "Refusing to record consumption for account {AccountId}: {Unknown}",
                consumptionEvent.AccountId,
                unknown
            );
            return new SaveConsumptionResult(SaveConsumptionResultCode.UnknownUsage, unknown);
        }

        var operationContext = operationContextAccessor.Current;
        if (operationContext is null)
        {
            // Not recoverable by retrying, and not something to paper over with a generated context:
            // a made-up identity is what let every retry charge again. The host that opened this
            // work has to open a scope for it.
            logger.LogError(
                "No operation context while recording consumption for account {AccountId}. "
                    + "Nothing was recorded.",
                consumptionEvent.AccountId
            );
            return new SaveConsumptionResult(
                SaveConsumptionResultCode.Failed,
                "No ambient operation context."
            );
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stamped = consumptionEvent.Stamp(
            operationContext.CorrelationId,
            IdempotencyKeyFactory.Create(
                operationContext,
                consumptionEvent.Operation,
                consumptionEvent.Unit,
                consumptionEvent.GrantId
            ),
            finalizedUtc: finalized ? now : null,
            outcome: finalized ? ConsumptionOutcome.Settled : null
        );

        try
        {
            // A step run again under the same scope finds its own earlier row. What it does with
            // it depends on how that row ended.
            var existing = await RetryPolicy.ExecuteAsync(
                token => FindByKeyAsync(stamped, token),
                _retryOptions,
                ct
            );

            if (existing is { Outcome: ConsumptionOutcome.Released })
            {
                // The earlier attempt gave its hold back, so nothing was charged. This attempt is
                // real work and holds again, on the same row: the key is unique, and a second row
                // under it can never be written.
                existing.Quantity = stamped.Quantity;
                existing.UtcTimestamp = stamped.UtcTimestamp;
                existing.CorrelationId = stamped.CorrelationId;
                existing.FinalizedUtc = stamped.FinalizedUtc;
                existing.Outcome = stamped.Outcome;

                await RetryPolicy.ExecuteAsync(
                    token => consumptionEventRepo.UpdateAsync(existing, token),
                    _retryOptions,
                    ct
                );

                logger.LogInformation(
                    "Consumption for account {AccountId} under idempotency key {IdempotencyKey} "
                        + "was released by an earlier attempt; holding it again for this one.",
                    stamped.AccountId,
                    stamped.IdempotencyKey
                );
                return new SaveConsumptionResult(SaveConsumptionResultCode.Success, null);
            }

            if (existing is not null)
            {
                // Still held, or already settled: a replay of work already accounted for.
                logger.LogInformation(
                    "Consumption for account {AccountId} was already recorded under idempotency key "
                        + "{IdempotencyKey}. Treating the retry as a no-op.",
                    stamped.AccountId,
                    stamped.IdempotencyKey
                );
                return new SaveConsumptionResult(
                    SaveConsumptionResultCode.Success,
                    "Already recorded."
                );
            }

            // Not retried on a DbUpdateException: the entity stays tracked after a failed insert,
            // so a second attempt fails on the tracker rather than the database, and the duplicate
            // it most likely was never gets recognised below.
            await RetryPolicy.ExecuteAsync(
                token => consumptionEventRepo.CreateAsync(stamped.ToEntity(), token),
                _retryOptions with
                {
                    ShouldRetry = ex =>
                        ex is not OperationCanceledException and not DbUpdateException,
                    OnRetry = (attempt, ex, delay) =>
                        logger.LogWarning(
                            ex,
                            "Retrying consumption write attempt {Attempt} in {Delay} for account {AccountId} (user {UserId})",
                            attempt,
                            delay,
                            stamped.AccountId,
                            stamped.UserId
                        ),
                },
                ct
            );
        }
        catch (Exception ex)
        {
            // Two attempts writing the same key at once: the loser's insert fails on the unique
            // index. That's the winner's row, not a failure. Classified by looking for the row
            // rather than by decoding a provider's error number, which would put SQL Server
            // specifically into this project.
            if (ex is DbUpdateException && await AlreadyRecordedAsync(stamped, ct))
            {
                logger.LogInformation(
                    "Consumption for account {AccountId} was already recorded under idempotency key "
                        + "{IdempotencyKey}. Treating the retry as a no-op.",
                    stamped.AccountId,
                    stamped.IdempotencyKey
                );
                return new SaveConsumptionResult(
                    SaveConsumptionResultCode.Success,
                    "Already recorded."
                );
            }

            logger.LogError(
                ex,
                "Failed to save consumption for account {AccountId}. Event: {@ConsumptionEvent}",
                stamped.AccountId,
                stamped
            );
            return new SaveConsumptionResult(SaveConsumptionResultCode.Failed, ex.Message);
        }

        return new SaveConsumptionResult(SaveConsumptionResultCode.Success, null);
    }

    private async Task<List<ConsumptionEventEntity>> FindOutstandingAsync(
        Guid accountId,
        OperationContext operationContext,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct
    )
    {
        var prefix = IdempotencyKeyFactory.Prefix(operationContext, operation, unit);

        return await RetryPolicy.ExecuteAsync(
            token =>
                consumptionEventRepo.ListAsync(
                    e =>
                        e.AccountId == accountId
                        && e.FinalizedUtc == null
                        && e.IdempotencyKey.StartsWith(prefix),
                    cancellationToken: token
                ),
            _retryOptions,
            ct
        );
    }

    /// <summary>
    /// Resolves every outstanding reservation for this operation.
    /// </summary>
    /// <param name="quantityByGrant">
    /// The settled charge per grant, or null to release everything. A grant named here with no
    /// reservation gets a new settled row -- that is a document that outgrew the grant it was
    /// reserved against drawing the remainder from the next one.
    /// </param>
    private async Task<SettleConsumptionResult> ResolveAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long>? quantityByGrant,
        CancellationToken ct
    )
    {
        var operationContext = operationContextAccessor.Current;
        if (operationContext is null)
        {
            logger.LogError(
                "No operation context while resolving reservations for account {AccountId}. "
                    + "The hold stays until it expires.",
                accountId
            );
            return new SettleConsumptionResult(
                SettleConsumptionResultCode.Failed,
                "No ambient operation context."
            );
        }

        try
        {
            var outstanding = await FindOutstandingAsync(
                accountId,
                operationContext,
                operation,
                unit,
                ct
            );

            if (outstanding.Count == 0)
            {
                // A replay whose reservations were already resolved, or a resolve for work that was
                // never reserved. Neither is an error, and neither may invent a row: with nothing
                // outstanding, writing the split would charge again for work already charged.
                logger.LogInformation(
                    "No outstanding reservations to resolve for account {AccountId} under {Operation}/{Unit}.",
                    accountId,
                    operation,
                    unit
                );
                return new SettleConsumptionResult(SettleConsumptionResultCode.Success, null);
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var settled = 0L;
            var rowCount = 0;

            foreach (var reservation in outstanding)
            {
                var charge =
                    quantityByGrant?.TryGetValue(reservation.GrantId, out var q) == true
                        ? (long?)q
                        : null;

                // A reservation whose grant no longer appears in the split is released rather than
                // settled at zero: it keeps its original quantity, so what was held and for how long
                // stays readable during a billing dispute.
                reservation.Quantity = charge ?? reservation.Quantity;
                reservation.FinalizedUtc = now;
                reservation.Outcome = charge is null
                    ? ConsumptionOutcome.Released
                    : ConsumptionOutcome.Settled;

                settled += charge ?? 0;
                rowCount++;

                await RetryPolicy.ExecuteAsync(
                    token => consumptionEventRepo.UpdateAsync(reservation, token),
                    _retryOptions,
                    ct
                );
            }

            foreach (var (grantId, quantity) in NewlyDrawnGrants(outstanding, quantityByGrant))
            {
                var template =
                    outstanding.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Cannot charge grant {grantId} for account {accountId}: no reservation "
                            + "exists to take the provider and tags from."
                    );

                await RetryPolicy.ExecuteAsync(
                    token =>
                        consumptionEventRepo.CreateAsync(
                            SpilloverRow(template, operationContext, grantId, quantity, now),
                            token
                        ),
                    _retryOptions,
                    ct
                );

                settled += quantity;
                rowCount++;
            }

            return new SettleConsumptionResult(
                SettleConsumptionResultCode.Success,
                null,
                settled,
                rowCount
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to resolve reservations for account {AccountId} under {Operation}/{Unit}.",
                accountId,
                operation,
                unit
            );
            return new SettleConsumptionResult(SettleConsumptionResultCode.Failed, ex.Message);
        }
    }

    private static IEnumerable<KeyValuePair<Guid, long>> NewlyDrawnGrants(
        List<ConsumptionEventEntity> outstanding,
        IReadOnlyDictionary<Guid, long>? quantityByGrant
    ) =>
        quantityByGrant is null
            ? []
            : quantityByGrant.Where(kv => !outstanding.Any(r => r.GrantId == kv.Key));

    /// <summary>
    /// A settled row on a grant this operation never reserved against.
    /// </summary>
    /// <remarks>
    /// Copied from a reservation rather than rebuilt from arguments, so the provider, user and tags
    /// describe the same piece of work. Its idempotency key is derived the same way every other row's
    /// is, which is what makes a replay recognise this row rather than write a second one.
    /// </remarks>
    private static ConsumptionEventEntity SpilloverRow(
        ConsumptionEventEntity template,
        OperationContext operationContext,
        Guid grantId,
        long quantity,
        DateTime now
    ) =>
        new()
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = template.AccountId,
            Quantity = quantity,
            Unit = template.Unit,
            Operation = template.Operation,
            Provider = template.Provider,
            UtcTimestamp = template.UtcTimestamp,
            CorrelationId = template.CorrelationId,
            IdempotencyKey = IdempotencyKeyFactory.Create(
                operationContext,
                template.Operation,
                template.Unit,
                grantId
            ),
            GrantId = grantId,
            FinalizedUtc = now,
            Outcome = ConsumptionOutcome.Settled,
            UserId = template.UserId,
            TagsJson = template.TagsJson,
        };

    private string? UnknownUsage(ConsumptionEvent consumptionEvent) =>
        (
            usageVocabulary.Knows(consumptionEvent.Operation),
            usageVocabulary.Knows(consumptionEvent.Unit)
        ) switch
        {
            (false, _) =>
                $"Operation '{consumptionEvent.Operation}' is not a registered usage operation.",
            (_, false) => $"Unit '{consumptionEvent.Unit}' is not a registered usage unit.",
            _ => null,
        };

    private Task<ConsumptionEventEntity?> FindByKeyAsync(
        ConsumptionEvent stamped,
        CancellationToken ct
    ) =>
        consumptionEventRepo.GetAsync(
            e => e.AccountId == stamped.AccountId && e.IdempotencyKey == stamped.IdempotencyKey,
            cancellationToken: ct
        );

    private async Task<bool> AlreadyRecordedAsync(ConsumptionEvent stamped, CancellationToken ct)
    {
        try
        {
            return await FindByKeyAsync(stamped, ct) is not null;
        }
        catch (Exception ex)
        {
            // If we cannot tell, say we cannot tell. Reporting success here would hide the very
            // thing this method exists to distinguish.
            logger.LogWarning(
                ex,
                "Could not check whether consumption for account {AccountId} was already recorded.",
                stamped.AccountId
            );
            return false;
        }
    }
}
