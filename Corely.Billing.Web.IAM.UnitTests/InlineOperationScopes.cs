using Corely.IAM.Web.Services;

namespace Corely.Billing.Web.IAM.UnitTests;

/// <summary>
/// Runs each IAM operation on the test's own services, signed in, so the components and IAM's
/// PermissionView see the test's mocks.
/// </summary>
internal sealed class InlineOperationScopes(IServiceProvider services) : IOperationScopes
{
    public bool HasSession => true;

    public async Task<T?> RunAsync<T>(Func<IServiceProvider, Task<T>> work) => await work(services);

    public async Task<bool> RunAsync(Func<IServiceProvider, Task> work)
    {
        await work(services);
        return true;
    }
}
