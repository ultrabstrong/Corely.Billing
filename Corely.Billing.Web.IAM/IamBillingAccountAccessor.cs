using Corely.IAM.Web.Services;

namespace Corely.Billing.Web.IAM;

internal sealed class IamBillingAccountAccessor(IBlazorUserContextAccessor userContextAccessor)
    : IBillingAccountAccessor
{
    public async Task<Guid?> GetAccountIdAsync() =>
        (await userContextAccessor.GetUserContextAsync())?.CurrentAccount?.Id;
}
