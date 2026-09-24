using Corely.Billing.Web;

namespace Corely.Billing.Demos.Portal;

internal sealed class DemoAccountAccessor : IBillingAccountAccessor
{
    public Task<Guid?> GetAccountIdAsync() => Task.FromResult<Guid?>(DemoUsage.AccountId);
}
