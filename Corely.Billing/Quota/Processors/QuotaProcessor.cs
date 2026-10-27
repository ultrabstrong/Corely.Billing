using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Quota.Mappers;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Processors;
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
    IRegistrationProcessor registrationProcessor,
    IUsageVocabulary vocabulary,
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
    private readonly IRegistrationProcessor _registrationProcessor =
        registrationProcessor.ThrowIfNull(nameof(registrationProcessor));
    private readonly IUsageVocabulary _vocabulary = vocabulary.ThrowIfNull(nameof(vocabulary));
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));
    private readonly ILogger<QuotaProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (WrongKind(operation, GrantKind.Consumable) is { } refused)
            return new GetBalanceResult(QuotaReadResultCode.WrongKindError, refused, null, []);

        var context = await LoadAsync(accountId, operation, ct);
        var used = context.Totals.ToDictionary(t => t.GrantId, t => t.TotalConsumedQuantity);
        foreach (var grant in context.Grants)
            grant.WithUse(used.GetValueOrDefault(grant.GrantId));

        return new GetBalanceResult(
            QuotaReadResultCode.Success,
            string.Empty,
            context.Grants.Any(g => g.Quantity is null)
                ? null
                : context.Grants.Sum(g => g.Remaining ?? 0),
            context.Grants
        );
    }

    public async Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (WrongKind(operation, GrantKind.Allowance) is { } refused)
            return new GetAllowanceResult(QuotaReadResultCode.WrongKindError, refused, null, []);

        var grants = await ActiveAsync(accountId, operation, GrantKind.Allowance, ct);
        return new GetAllowanceResult(
            QuotaReadResultCode.Success,
            string.Empty,
            grants.Any(g => g.Quantity is null) ? null : grants.Sum(g => g.Quantity!.Value),
            grants
        );
    }

    public async Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (WrongKind(operation, GrantKind.Feature) is { } refused)
            return new HasFeatureResult(QuotaReadResultCode.WrongKindError, refused, false, []);

        var grants = await ActiveAsync(accountId, operation, GrantKind.Feature, ct);
        return new HasFeatureResult(
            QuotaReadResultCode.Success,
            string.Empty,
            grants.Count > 0,
            grants
        );
    }

    public async Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (WrongKind(operation, GrantKind.Capacity) is { } refused)
            return new GetCapacityResult(QuotaReadResultCode.WrongKindError, refused, null, 0, []);

        var grants = await ActiveAsync(accountId, operation, GrantKind.Capacity, ct);
        var inUse = await _registrationProcessor.CountInUseAsync(
            [.. grants.Select(g => g.GrantId)],
            ct
        );
        foreach (var grant in grants)
            grant.InUse = inUse.GetValueOrDefault(grant.GrantId);

        return new GetCapacityResult(
            QuotaReadResultCode.Success,
            string.Empty,
            grants.Any(g => g.Quantity is null) ? null : grants.Sum(g => g.Quantity!.Value),
            grants.Sum(g => g.InUse ?? 0),
            grants
        );
    }

    private string? WrongKind(UsageOperation operation, GrantKind kind) =>
        _vocabulary.Definition(operation) is not { } definition
            ? $"Operation '{operation}' is not a registered usage operation."
        : definition.Kind != kind
            ? $"Operation '{operation}' is declared as {definition.Kind}, not {kind}."
        : null;

    private Task<List<Grant>> ActiveAsync(
        Guid accountId,
        UsageOperation operation,
        GrantKind kind,
        CancellationToken ct
    ) =>
        _grantProcessor.ListActiveGrantsAsync(
            accountId,
            operation,
            kind,
            _timeProvider.GetUtcNow().UtcDateTime,
            ct: ct
        );

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

        var context = await LoadAsync(request.AccountId, request.Operation, ct);
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
            await LoadAsync(
                request.AccountId,
                request.Operation,
                ct,
                [.. outstanding.Select(h => h.GrantId)]
            )
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
        CancellationToken ct,
        IReadOnlyCollection<Guid>? heldGrantIds = null
    )
    {
        var grants = await _grantProcessor.ListActiveGrantsAsync(
            accountId,
            operation,
            GrantKind.Consumable,
            _timeProvider.GetUtcNow().UtcDateTime,
            heldGrantIds,
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
