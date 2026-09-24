using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Operations;
using Corely.Billing.Resilience;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Processors;

internal class ConsumptionProcessor(
    IRepo<ConsumptionEventEntity> consumptionEventRepo,
    IOperationContextAccessor operationContextAccessor,
    IValidationProvider validationProvider,
    TimeProvider timeProvider,
    ILogger<ConsumptionProcessor> logger
) : IConsumptionProcessor
{
    private static readonly RetryOptions _retryOptions = new()
    {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromMilliseconds(200),
        MaxDelay = TimeSpan.FromSeconds(3),
        FastFirst = true,
        ShouldRetry = ex => ex is not OperationCanceledException,
    };

    private readonly IRepo<ConsumptionEventEntity> _consumptionEventRepo =
        consumptionEventRepo.ThrowIfNull(nameof(consumptionEventRepo));
    private readonly IOperationContextAccessor _operationContextAccessor =
        operationContextAccessor.ThrowIfNull(nameof(operationContextAccessor));
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));
    private readonly ILogger<ConsumptionProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<ReserveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(consumptionEvent, nameof(consumptionEvent));

        var validation = _validationProvider.ValidateAndLog(consumptionEvent);
        if (!validation.IsValid)
        {
            return new ReserveConsumptionResult(
                ReserveConsumptionResultCode.ValidationError,
                validation.Message
            );
        }

        var operationContext = _operationContextAccessor.Current;
        if (operationContext is null)
        {
            _logger.LogError(
                "No operation context while reserving consumption for account {AccountId}. "
                    + "Nothing was recorded.",
                consumptionEvent.AccountId
            );
            return new ReserveConsumptionResult(
                ReserveConsumptionResultCode.NotRecordedError,
                "No ambient operation context."
            );
        }

        var reservation = consumptionEvent.ToEntity();
        reservation.ConsumptionId =
            reservation.ConsumptionId == Guid.Empty
                ? Guid.CreateVersion7()
                : reservation.ConsumptionId;
        reservation.CorrelationId = operationContext.CorrelationId;
        reservation.IdempotencyKey = IdempotencyKeyFactory.Create(
            operationContext,
            reservation.Operation,
            reservation.Unit,
            reservation.GrantId
        );
        reservation.FinalizedUtc = null;
        reservation.Outcome = null;

        try
        {
            var existing = await RetryPolicy.ExecuteAsync(
                token => FindByKeyAsync(reservation, token),
                _retryOptions,
                ct
            );

            if (existing is { Outcome: ConsumptionOutcome.Released })
            {
                existing.Quantity = reservation.Quantity;
                existing.UtcTimestamp = reservation.UtcTimestamp;
                existing.CorrelationId = reservation.CorrelationId;
                existing.FinalizedUtc = null;
                existing.Outcome = null;

                await RetryPolicy.ExecuteAsync(
                    token => _consumptionEventRepo.UpdateAsync(existing, token),
                    _retryOptions,
                    ct
                );

                _logger.LogInformation(
                    "Consumption for account {AccountId} under idempotency key {IdempotencyKey} "
                        + "was released by an earlier attempt; holding it again for this one.",
                    reservation.AccountId,
                    reservation.IdempotencyKey
                );
                return new ReserveConsumptionResult(
                    ReserveConsumptionResultCode.Success,
                    string.Empty
                );
            }

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Consumption for account {AccountId} was already recorded under idempotency key "
                        + "{IdempotencyKey}. Treating the retry as a no-op.",
                    reservation.AccountId,
                    reservation.IdempotencyKey
                );
                return new ReserveConsumptionResult(
                    ReserveConsumptionResultCode.Success,
                    "Already recorded."
                );
            }

            await RetryPolicy.ExecuteAsync(
                token => _consumptionEventRepo.CreateAsync(reservation, token),
                _retryOptions with
                {
                    ShouldRetry = ex =>
                        ex is not OperationCanceledException and not DbUpdateException,
                    OnRetry = (attempt, ex, delay) =>
                        _logger.LogWarning(
                            ex,
                            "Retrying consumption write attempt {Attempt} in {Delay} for account {AccountId} (user {UserId})",
                            attempt,
                            delay,
                            reservation.AccountId,
                            reservation.UserId
                        ),
                },
                ct
            );
        }
        catch (Exception ex)
        {
            if (ex is DbUpdateException && await AlreadyRecordedAsync(reservation, ct))
            {
                _logger.LogInformation(
                    "Consumption for account {AccountId} was already recorded under idempotency key "
                        + "{IdempotencyKey}. Treating the retry as a no-op.",
                    reservation.AccountId,
                    reservation.IdempotencyKey
                );
                return new ReserveConsumptionResult(
                    ReserveConsumptionResultCode.Success,
                    "Already recorded."
                );
            }

            _logger.LogError(
                ex,
                "Failed to reserve consumption for account {AccountId}. Event: {@ConsumptionEvent}",
                reservation.AccountId,
                consumptionEvent
            );
            return new ReserveConsumptionResult(
                ReserveConsumptionResultCode.NotRecordedError,
                ex.Message
            );
        }

        return new ReserveConsumptionResult(ReserveConsumptionResultCode.Success, string.Empty);
    }

    public Task<ResolveConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(quantityByGrant, nameof(quantityByGrant));
        return ResolveAsync(accountId, operation, unit, quantityByGrant, ct);
    }

    public Task<ResolveConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) => ResolveAsync(accountId, operation, unit, quantityByGrant: null, ct);

    public async Task<List<ConsumptionEvent>?> ListOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        var operationContext = _operationContextAccessor.Current;
        if (operationContext is null)
        {
            _logger.LogError(
                "No operation context while reading outstanding reservations for account "
                    + "{AccountId}.",
                accountId
            );
            return null;
        }

        var outstanding = await FindOutstandingAsync(
            accountId,
            operationContext,
            operation,
            unit,
            ct
        );
        return [.. outstanding.Select(e => e.ToModel())];
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
                _consumptionEventRepo.ListAsync(
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

    private async Task<ResolveConsumptionResult> ResolveAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long>? quantityByGrant,
        CancellationToken ct
    )
    {
        var operationContext = _operationContextAccessor.Current;
        if (operationContext is null)
        {
            _logger.LogError(
                "No operation context while resolving reservations for account {AccountId}. "
                    + "The hold stays until it expires.",
                accountId
            );
            return new ResolveConsumptionResult(
                ResolveConsumptionResultCode.NotRecordedError,
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
                _logger.LogInformation(
                    "No outstanding reservations to resolve for account {AccountId} under {Operation}/{Unit}.",
                    accountId,
                    operation,
                    unit
                );
                return new ResolveConsumptionResult(
                    ResolveConsumptionResultCode.Success,
                    string.Empty
                );
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settled = 0L;
            var rowCount = 0;

            foreach (var reservation in outstanding)
            {
                var charge =
                    quantityByGrant?.TryGetValue(reservation.GrantId, out var q) == true
                        ? (long?)q
                        : null;

                reservation.Quantity = charge ?? reservation.Quantity;
                reservation.FinalizedUtc = now;
                reservation.Outcome = charge is null
                    ? ConsumptionOutcome.Released
                    : ConsumptionOutcome.Settled;

                settled += charge ?? 0;
                rowCount++;

                await RetryPolicy.ExecuteAsync(
                    token => _consumptionEventRepo.UpdateAsync(reservation, token),
                    _retryOptions,
                    ct
                );
            }

            foreach (var (grantId, quantity) in NewlyDrawnGrants(outstanding, quantityByGrant))
            {
                var template = outstanding[0];

                await RetryPolicy.ExecuteAsync(
                    token =>
                        _consumptionEventRepo.CreateAsync(
                            template.ToSpillover(operationContext, grantId, quantity, now),
                            token
                        ),
                    _retryOptions,
                    ct
                );

                settled += quantity;
                rowCount++;
            }

            return new ResolveConsumptionResult(
                ResolveConsumptionResultCode.Success,
                string.Empty,
                settled,
                rowCount
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to resolve reservations for account {AccountId} under {Operation}/{Unit}.",
                accountId,
                operation,
                unit
            );
            return new ResolveConsumptionResult(
                ResolveConsumptionResultCode.NotRecordedError,
                ex.Message
            );
        }
    }

    private static IEnumerable<KeyValuePair<Guid, long>> NewlyDrawnGrants(
        List<ConsumptionEventEntity> outstanding,
        IReadOnlyDictionary<Guid, long>? quantityByGrant
    ) =>
        quantityByGrant is null
            ? []
            : quantityByGrant.Where(kv => !outstanding.Any(r => r.GrantId == kv.Key));

    private Task<ConsumptionEventEntity?> FindByKeyAsync(
        ConsumptionEventEntity reservation,
        CancellationToken ct
    ) =>
        _consumptionEventRepo.GetAsync(
            e =>
                e.AccountId == reservation.AccountId
                && e.IdempotencyKey == reservation.IdempotencyKey,
            cancellationToken: ct
        );

    private async Task<bool> AlreadyRecordedAsync(
        ConsumptionEventEntity reservation,
        CancellationToken ct
    )
    {
        try
        {
            return await FindByKeyAsync(reservation, ct) is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not check whether consumption for account {AccountId} was already recorded.",
                reservation.AccountId
            );
            return false;
        }
    }
}
