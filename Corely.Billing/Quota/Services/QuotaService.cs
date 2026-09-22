using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Services;
using Corely.Billing.Quota.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Quota.Services;

internal class QuotaService(
    IGrantReader grantReader,
    IConsumptionReader consumptionReader,
    IConsumptionWriter consumptionWriter,
    IGrantSelectionPolicy grantSelectionPolicy,
    TimeProvider timeProvider,
    ILogger<QuotaService> logger
) : IQuotaService
{
    public async Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        try
        {
            var context = await LoadAsync(accountId, operation, unit, tags: null, ct);

            if (context is null || context.Grants.Count == 0)
                return QuotaAvailability.Exhausted;

            // One page is the smallest question worth asking. Anything larger would be guessing at
            // a size nothing knows yet, and this check exists to catch the obvious no.
            var split = grantSelectionPolicy.Split(context.Grants, context.Totals, quantity: 1);

            return split.Shortfall > 0 ? QuotaAvailability.Exhausted : QuotaAvailability.Available;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallowed on purpose, and the only place in this service that does it. The caller is
            // the workflow initializer: a database blip here must not stop every document in the
            // system from starting, when the accurate check a step later would have caught it.
            logger.LogWarning(
                ex,
                "Could not determine {Operation}/{Unit} quota availability for AccountId {AccountId}. "
                    + "Letting the job start.",
                operation,
                unit,
                accountId
            );
            return QuotaAvailability.Unknown;
        }
    }

    public async Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            request.Tags,
            ct
        );

        if (context is null)
            return new ReserveQuotaResult(ReserveQuotaResultCode.Unauthorized, null);

        if (context.Grants.Count == 0)
            return new ReserveQuotaResult(ReserveQuotaResultCode.NoGrantAvailable, null);

        var split = grantSelectionPolicy.Split(context.Grants, context.Totals, request.Quantity);

        if (split.Shortfall > 0)
        {
            // Refused on the total across every valid grant, which is the honest meaning of
            // "insufficient quota" -- not "the one grant I picked was too small".
            logger.LogInformation(
                "Insufficient {Operation}/{Unit} quota for AccountId {AccountId}: {Requested} requested, "
                    + "{Short} short across {GrantCount} grants.",
                request.Operation,
                request.Unit,
                request.AccountId,
                request.Quantity,
                split.Shortfall,
                context.Grants.Count
            );
            return new ReserveQuotaResult(ReserveQuotaResultCode.InsufficientQuota, null);
        }

        foreach (var share in split.Shares)
        {
            var reservation = ConsumptionEvent.Create(
                accountId: request.AccountId,
                quantity: share.Quantity,
                unit: request.Unit,
                operation: request.Operation,
                provider: request.Provider,
                utcTimestamp: timeProvider.GetUtcNow().UtcDateTime,
                grantId: share.GrantId,
                userId: request.UserId,
                tags: request.Tags
            );

            if (!reservation.IsSuccess)
            {
                logger.LogError(
                    "Invalid consumption event reserving {Operation}/{Unit} for AccountId {AccountId}: {Message}",
                    request.Operation,
                    request.Unit,
                    request.AccountId,
                    reservation.Message
                );
                return new ReserveQuotaResult(
                    ReserveQuotaResultCode.NotRecorded,
                    reservation.Message
                );
            }

            var saveResult = await consumptionWriter.ReserveAsync(reservation.Value!, ct);
            if (saveResult.ResultCode != SaveConsumptionResultCode.Success)
            {
                // Partially reserved. The rows already written stay outstanding and are released
                // when the caller gives up, or expire if it dies -- either way the caller is told
                // the reservation failed, so the work does not start.
                logger.LogError(
                    "Could not reserve {Quantity} on grant {GrantId} for AccountId {AccountId}: "
                        + "{ResultCode} {Message}",
                    share.Quantity,
                    share.GrantId,
                    request.AccountId,
                    saveResult.ResultCode,
                    saveResult.Message
                );
                return new ReserveQuotaResult(
                    ReserveQuotaResultCode.NotRecorded,
                    saveResult.Message
                );
            }
        }

        return new ReserveQuotaResult(ReserveQuotaResultCode.Success, null, split.Shares);
    }

    public async Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var outstandingResult = await consumptionReader.GetOutstandingReservationsAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            ct
        );

        switch (outstandingResult.ResultCode)
        {
            case GetOutstandingReservationsResultCode.Unauthorized:
                return new SettleQuotaResult(
                    SettleQuotaResultCode.Unauthorized,
                    outstandingResult.Message
                );
            case GetOutstandingReservationsResultCode.Failed:
                return new SettleQuotaResult(
                    SettleQuotaResultCode.Failed,
                    outstandingResult.Message
                );
        }

        var outstanding = outstandingResult.Reservations ?? [];

        if (outstanding.Count == 0)
        {
            // A replay whose reservations were already settled. Metering treats that as a no-op, and
            // re-deriving a split for rows that no longer exist would only invent charges.
            return await ApplyAsync(request, new Dictionary<Guid, long>(), overdrawn: false, ct);
        }

        var context = await LoadAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            outstanding[0].Tags,
            ct
        );

        if (context is null)
            return new SettleQuotaResult(SettleQuotaResultCode.Unauthorized, null);

        var totalsWithoutOwnHolds = SubtractOwnReservations(context.Totals, outstanding);
        var split = grantSelectionPolicy.Split(
            context.Grants,
            totalsWithoutOwnHolds,
            request.ActualQuantity
        );
        var remainingRatio = RemainingRatio(
            context.Grants,
            totalsWithoutOwnHolds,
            request.ActualQuantity
        );

        var quantityByGrant = split.Shares.ToDictionary(a => a.GrantId, a => a.Quantity);
        var overdrawn = split.Shortfall > 0;

        if (overdrawn)
        {
            // Overdraft-once. The provider has already been paid for this work, so refusing now
            // means eating the cost, giving the customer nothing, and nothing stopping it recurring.
            // The last grant absorbs it and goes negative, which is what makes the overrun visible
            // rather than silently absent.
            var lastGrantId = split.Shares.LastOrDefault()?.GrantId ?? outstanding[^1].GrantId;

            quantityByGrant[lastGrantId] =
                quantityByGrant.GetValueOrDefault(lastGrantId) + split.Shortfall;

            logger.LogWarning(
                "Overdrawing grant {GrantId} by {Overdraft} settling {Operation}/{Unit} for "
                    + "AccountId {AccountId}. The work was already done and is being delivered.",
                lastGrantId,
                split.Shortfall,
                request.Operation,
                request.Unit,
                request.AccountId
            );
        }

        var result = await ApplyAsync(request, quantityByGrant, overdrawn, ct);
        return result.ResultCode == SettleQuotaResultCode.Success
            ? result with
            {
                RemainingRatio = remainingRatio,
            }
            : result;
    }

    private static double RemainingRatio(
        List<Grant> grants,
        List<GrantTotalConsumptions> totals,
        long charged
    )
    {
        var total = grants.Sum(g => g.Quantity);
        if (total <= 0)
            return 0;

        var consumed = totals.Sum(t => t.TotalConsumedQuantity) + charged;
        return Math.Clamp((double)(total - consumed) / total, 0, 1);
    }

    public async Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await consumptionWriter.ReleaseAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            ct
        );

        return ToSettleResult(result, overdrawn: false);
    }

    private async Task<SettleQuotaResult> ApplyAsync(
        SettleQuotaRequest request,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        bool overdrawn,
        CancellationToken ct
    )
    {
        var result = await consumptionWriter.SettleAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            quantityByGrant,
            ct
        );

        return ToSettleResult(result, overdrawn);
    }

    private static SettleQuotaResult ToSettleResult(
        SettleConsumptionResult result,
        bool overdrawn
    ) =>
        new(
            result.ResultCode switch
            {
                SettleConsumptionResultCode.Success => SettleQuotaResultCode.Success,
                SettleConsumptionResultCode.Unauthorized => SettleQuotaResultCode.Unauthorized,
                _ => SettleQuotaResultCode.Failed,
            },
            result.Message,
            result.SettledQuantity,
            overdrawn
        );

    /// <summary>
    /// Removes this operation's own holds from the per-grant totals.
    /// </summary>
    /// <remarks>
    /// Without this, settling a hundred-page document against a hold of one page would see that page
    /// as consumed and allocate the hundred around it -- the work would be competing with the
    /// reservation it took out for itself.
    /// </remarks>
    private static List<GrantTotalConsumptions> SubtractOwnReservations(
        List<GrantTotalConsumptions> totals,
        IReadOnlyList<ConsumptionEvent> outstanding
    ) =>
        [
            .. totals.Select(t =>
                t with
                {
                    TotalConsumedQuantity =
                        t.TotalConsumedQuantity
                        - outstanding.Where(o => o.GrantId == t.GrantId).Sum(o => o.Quantity),
                }
            ),
        ];

    private sealed record QuotaContext(List<Grant> Grants, List<GrantTotalConsumptions> Totals);

    /// <summary>The account's live grants and what has been drawn from each. Null when unauthorized.</summary>
    private async Task<QuotaContext?> LoadAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<string, string>? tags,
        CancellationToken ct
    )
    {
        var grantsResult = await grantReader.GetActiveGrantsAsync(
            accountId,
            timeProvider.GetUtcNow().UtcDateTime,
            operation,
            unit,
            tags,
            ct
        );

        if (grantsResult.ResultCode == GetActiveGrantsResultCode.Unauthorized)
            return null;

        var grants = grantsResult.Grants ?? [];
        if (grants.Count == 0)
            return new QuotaContext(grants, []);

        var totalsResult = await consumptionReader.GetGrantConsumptionTotalsAsync(
            accountId,
            [.. grants.Select(g => g.GrantId)],
            ct
        );

        return totalsResult.ResultCode == GetGrantConsumptionTotalsResultCode.Unauthorized
            ? null
            : new QuotaContext(grants, totalsResult.Totals ?? []);
    }
}
