namespace Corely.Billing.Web.UnitTests;

/// <summary>
/// Runs each operation on the test's own services, so a component test sees its mocks.
/// <c>BillingOperationScopesTests</c> covers the real scopes.
/// </summary>
internal sealed class InlineBillingOperationScopes(IServiceProvider services)
    : IBillingOperationScopes
{
    public async Task<T?> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => await work(services);

    public async Task<bool> RunAsync(Func<IServiceProvider, Task> work)
    {
        await work(services);
        return true;
    }
}
