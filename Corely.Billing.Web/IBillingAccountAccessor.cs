namespace Corely.Billing.Web;

public interface IBillingAccountAccessor
{
    Task<Guid?> GetAccountIdAsync();

    Task<Guid?> GetUserIdAsync() => Task.FromResult<Guid?>(null);
}
