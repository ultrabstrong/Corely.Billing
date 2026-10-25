using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Processors;

internal interface IQuotaProcessor
{
    Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    );

    Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
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
