using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class QuotaAuthorizationDecorator(
    IQuotaService inner,
    IAuthorizationProvider authorizationProvider,
    AccountReadScope accountReadScope
) : IQuotaService
{
    private const string DENIED = "Unauthorized to read quota";

    public async Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(accountId, AuthAction.Read)
            ? await inner.GetBalanceAsync(accountId, operation, ct)
            : new GetBalanceResult(QuotaReadResultCode.UnauthorizedError, DENIED, null, []);

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

    public async Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(accountId, AuthAction.Read)
            ? await inner.GetAllowanceAsync(accountId, operation, ct)
            : new GetAllowanceResult(QuotaReadResultCode.UnauthorizedError, DENIED, null, []);

    public async Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(accountId, AuthAction.Read)
            ? await inner.HasFeatureAsync(accountId, operation, ct)
            : new HasFeatureResult(QuotaReadResultCode.UnauthorizedError, DENIED, false, []);

    public async Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedAsync(accountId, AuthAction.Read)
            ? await inner.GetCapacityAsync(accountId, operation, ct)
            : new GetCapacityResult(QuotaReadResultCode.UnauthorizedError, DENIED, null, 0, []);

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedForRegistrationsAsync(request.AccountId)
            ? await inner.RegisterAsync(request, ct)
            : new RegisterResult(RegisterResultCode.UnauthorizedError, "Unauthorized to register");

    public async Task<ModifyResult> ReleaseRegistrationAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedForRegistrationsAsync(accountId)
            ? await inner.ReleaseRegistrationAsync(accountId, registrationId, ct)
            : new ModifyResult(
                ModifyResultCode.UnauthorizedError,
                "Unauthorized to release a registration"
            );

    public async Task<MoveRegistrationsResult> MoveRegistrationsAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    ) =>
        await IsAuthorizedForRegistrationsAsync(request.AccountId)
            ? await inner.MoveRegistrationsAsync(request, ct)
            : new MoveRegistrationsResult(
                MoveRegistrationsResultCode.UnauthorizedError,
                "Unauthorized to move registrations",
                []
            );

    public async Task<RetrieveListResult<Registration>> ListRegistrationsAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    ) =>
        await accountReadScope.CanReadAsync(
            request.AccountIds,
            BillingResourceTypes.REGISTRATION_RESOURCE_TYPE
        )
            ? await inner.ListRegistrationsAsync(request, ct)
            : new RetrieveListResult<Registration>(
                RetrieveResultCode.UnauthorizedError,
                "Unauthorized to list registrations",
                null
            );

    private async Task<bool> IsAuthorizedAsync(Guid accountId, AuthAction action) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            action,
            BillingResourceTypes.QUOTA_RESOURCE_TYPE
        );

    private async Task<bool> IsAuthorizedForRegistrationsAsync(Guid accountId) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Execute,
            BillingResourceTypes.REGISTRATION_RESOURCE_TYPE
        );
}
