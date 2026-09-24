using Corely.Billing.Web;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;
using Corely.IAM.Web.Services;

namespace Corely.Billing.Demos.WithIAM;

internal sealed class IamBillingAccountAccessor(
    IBlazorUserContextAccessor userContextAccessor,
    IAuthorizationProvider authorizationProvider
) : IBillingAccountAccessor
{
    public async Task<Guid?> GetAccountIdAsync() =>
        (await userContextAccessor.GetUserContextAsync())?.CurrentAccount?.Id;

    public Task<bool> CanManageGrantsAsync() =>
        authorizationProvider.IsAuthorizedAsync(AuthAction.Update, DemoUsage.GRANTS_RESOURCE);
}
