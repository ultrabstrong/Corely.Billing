using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Quota.Extensions;
using Corely.Billing.Quota.Mappers;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Quota.Processors;

internal class QuotaProcessor(
    IGrantProcessor grantProcessor,
    IConsumptionProcessor consumptionProcessor,
    IConsumptionReportProcessor consumptionReportProcessor,
    IGrantSelectionPolicy grantSelectionPolicy,
    IValidationProvider validationProvider,
    TimeProvider timeProvider,
    ILogger<QuotaProcessor> logger
) : IQuotaProcessor
{
    private readonly IGrantProcessor _grantProcessor = grantProcessor.ThrowIfNull(
        nameof(grantProcessor)
    );
    private readonly IConsumptionProcessor _consumptionProcessor = consumptionProcessor.ThrowIfNull(
        nameof(consumptionProcessor)
    );
    private readonly IConsumptionReportProcessor _consumptionReportProcessor =
        consumptionReportProcessor.ThrowIfNull(nameof(consumptionReportProcessor));
    private readonly IGrantSelectionPolicy _grantSelectionPolicy = grantSelectionPolicy.ThrowIfNull(
        nameof(grantSelectionPolicy)
    );
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));
    private readonly ILogger<QuotaProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        try
        {
            var context = await LoadAsync(accountId, operation, unit, ct);
            if (context.Grants.Count == 0)
                return QuotaAvailability.Exhausted;

            var split = _grantSelectionPolicy.Split(context.Grants, context.Totals, quantity: 1);

            return split.Shortfall > 0 ? QuotaAvailability.Exhausted : QuotaAvailability.Available;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not determine {Operation}/{Unit} quota availability for AccountId {AccountId}. "
                    + "Letting the work start.",
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
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var validation = _validationProvider.ValidateAndLog(request);
        if (!validation.IsValid)
            return new ReserveQuotaResult(
                ReserveQuotaResultCode.ValidationError,
                validation.Message
            );

        var context = await LoadAsync(request.AccountId, request.Operation, request.Unit, ct);
        if (context.Grants.Count == 0)
        {
            return new ReserveQuotaResult(
                ReserveQuotaResultCode.NoGrantAvailableError,
                "No live grant covers this operation and unit"
            );
        }

        var split = _grantSelectionPolicy.Split(context.Grants, context.Totals, request.Quantity);

        if (split.Shortfall > 0)
        {
            _logger.LogInformation(
                "Insufficient {Operation}/{Unit} quota for AccountId {AccountId}: {Requested} requested, "
                    + "{Short} short across {GrantCount} grants.",
                request.Operation,
                request.Unit,
                request.AccountId,
                request.Quantity,
                split.Shortfall,
                context.Grants.Count
            );
            return new ReserveQuotaResult(
                ReserveQuotaResultCode.InsufficientQuotaError,
                "Insufficient quota across the account's live grants"
            );
        }

        foreach (var share in split.Shares)
        {
            var reservation = new ConsumptionEvent
            {
                AccountId = request.AccountId,
                GrantId = share.GrantId,
                Operation = request.Operation,
                Unit = request.Unit,
                Quantity = share.Quantity,
                Provider = request.Provider,
                UtcTimestamp = _timeProvider.GetUtcNow().UtcDateTime,
                UserId = request.UserId,
                Tags = request.Tags,
            };

            var reserved = await _consumptionProcessor.ReserveAsync(reservation, ct);
            if (reserved.ResultCode != ReserveConsumptionResultCode.Success)
            {
                _logger.LogError(
                    "Could not reserve {Quantity} on grant {GrantId} for AccountId {AccountId}: "
                        + "{ResultCode} {Message}",
                    share.Quantity,
                    share.GrantId,
                    request.AccountId,
                    reserved.ResultCode,
                    reserved.Message
                );
                return new ReserveQuotaResult(
                    ReserveQuotaResultCode.NotRecordedError,
                    reserved.Message
                );
            }
        }

        return new ReserveQuotaResult(ReserveQuotaResultCode.Success, string.Empty, split.Shares);
    }

    public async Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var outstanding = await _consumptionProcessor.ListOutstandingReservationsAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            ct
        );
        if (outstanding is null)
        {
            return new SettleQuotaResult(
                SettleQuotaResultCode.NotRecordedError,
                "No ambient operation context."
            );
        }

        if (outstanding.Count == 0)
        {
            return await ApplyAsync(request, new Dictionary<Guid, long>(), overdrawn: false, ct);
        }

        var context = (
            await LoadAsync(request.AccountId, request.Operation, request.Unit, ct)
        ).WithoutHolds(outstanding);
        var split = _grantSelectionPolicy.Split(
            context.Grants,
            context.Totals,
            request.ActualQuantity
        );
        var remainingRatio = context.RemainingRatio(request.ActualQuantity);

        var quantityByGrant = split.Shares.ToDictionary(s => s.GrantId, s => s.Quantity);
        var overdrawn = split.Shortfall > 0;

        if (overdrawn)
        {
            var lastGrantId = split.Shares.LastOrDefault()?.GrantId ?? outstanding[^1].GrantId;

            quantityByGrant[lastGrantId] =
                quantityByGrant.GetValueOrDefault(lastGrantId) + split.Shortfall;

            _logger.LogWarning(
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

    public async Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var result = await _consumptionProcessor.ReleaseAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            ct
        );

        return result.ToSettleQuotaResult(overdrawn: false);
    }

    private async Task<SettleQuotaResult> ApplyAsync(
        SettleQuotaRequest request,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        bool overdrawn,
        CancellationToken ct
    )
    {
        var result = await _consumptionProcessor.SettleAsync(
            request.AccountId,
            request.Operation,
            request.Unit,
            quantityByGrant,
            ct
        );

        return result.ToSettleQuotaResult(overdrawn);
    }

    private async Task<QuotaContext> LoadAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct
    )
    {
        var grants = await _grantProcessor.ListActiveGrantsAsync(
            accountId,
            operation,
            unit,
            _timeProvider.GetUtcNow().UtcDateTime,
            ct
        );
        if (grants.Count == 0)
            return new QuotaContext(grants, []);

        var totals = await _consumptionReportProcessor.GetGrantConsumptionTotalsAsync(
            accountId,
            [.. grants.Select(g => g.GrantId)],
            ct
        );

        return new QuotaContext(grants, totals);
    }
}
