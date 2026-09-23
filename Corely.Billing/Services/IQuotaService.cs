using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Services;

public interface IQuotaService
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
