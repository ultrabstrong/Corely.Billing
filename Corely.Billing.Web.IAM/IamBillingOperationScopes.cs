using Corely.IAM.Web.Services;

namespace Corely.Billing.Web.IAM;

// IAM's operation scopes sign in with the tab's token and reload a page whose session ended, so
// Billing's components get both from the one place that owns the token.
internal sealed class IamBillingOperationScopes(IOperationScopes operationScopes)
    : IBillingOperationScopes
{
    public Task<T?> RunAsync<T>(Func<IServiceProvider, Task<T>> work) =>
        operationScopes.RunAsync(work);

    public Task<bool> RunAsync(Func<IServiceProvider, Task> work) => operationScopes.RunAsync(work);
}
