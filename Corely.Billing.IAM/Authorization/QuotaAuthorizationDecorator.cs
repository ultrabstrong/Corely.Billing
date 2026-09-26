using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class QuotaAuthorizationDecorator(
    IQuotaService inner,
    IAuthorizationProvider authorizationProvider
) : IQuotaService
{
    public async Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(accountId, AuthAction.Read)
            ? await inner.GetAvailabilityAsync(accountId, operation, unit, ct)
            : QuotaAvailability.Unauthorized;

    public async Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(request.AccountId, AuthAction.Execute)
            ? await inner.ReserveAsync(request, ct)
            : new ReserveQuotaResult(
                ReserveQuotaResultCode.UnauthorizedError,
                "Unauthorized to reserve quota"
            );

    public async Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(request.AccountId, AuthAction.Execute)
            ? await inner.SettleAsync(request, ct)
            : new SettleQuotaResult(
                SettleQuotaResultCode.UnauthorizedError,
                "Unauthorized to settle quota"
            );

    public async Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(request.AccountId, AuthAction.Execute)
            ? await inner.ReleaseAsync(request, ct)
            : new SettleQuotaResult(
                SettleQuotaResultCode.UnauthorizedError,
                "Unauthorized to release quota"
            );

    private async Task<bool> IsAuthorizedAsync(Guid accountId, AuthAction action) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            action,
            BillingResourceTypes.QUOTA_RESOURCE_TYPE
        );
}
