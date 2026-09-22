using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Usage;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class QuotaService(IQuotaProcessor quotaProcessor) : IQuotaService
{
    private readonly IQuotaProcessor _quotaProcessor = quotaProcessor.ThrowIfNull(
        nameof(quotaProcessor)
    );

    public Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) => _quotaProcessor.GetAvailabilityAsync(accountId, operation, unit, ct);

    public Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.ReserveAsync(request, ct);

    public Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.SettleAsync(request, ct);

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) => _quotaProcessor.ReleaseAsync(request, ct);
}
