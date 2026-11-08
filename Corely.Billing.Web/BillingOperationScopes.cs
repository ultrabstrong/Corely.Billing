using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web;

// The host's account accessor is what signs a scope in, whatever it reads the account from.
internal sealed class BillingOperationScopes(IServiceScopeFactory scopeFactory)
    : IBillingOperationScopes
{
    public async Task<T?> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var scope = scopeFactory.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<IBillingAccountAccessor>()
            .GetAccountIdAsync();
        return await work(scope.ServiceProvider);
    }

    public async Task<bool> RunAsync(Func<IServiceProvider, Task> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return await RunAsync(async services =>
        {
            await work(services);
            return true;
        });
    }
}
