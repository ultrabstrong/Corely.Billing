namespace Corely.Billing.Web;

public interface IBillingAccountAccessor
{
    Task<Guid?> GetAccountIdAsync();

    Task<bool> CanManageGrantsAsync() => Task.FromResult(true);
}
