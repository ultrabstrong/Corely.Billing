namespace Corely.Billing.Web;

public interface IBillingAccountAccessor
{
    Task<Guid?> GetAccountIdAsync();
}
