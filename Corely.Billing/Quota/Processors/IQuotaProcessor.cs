using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Processors;

internal interface IQuotaProcessor
{
    Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );

    Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    );

    Task<SettleQuotaResult> SettleAsync(SettleQuotaRequest request, CancellationToken ct = default);

    Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    );
}
