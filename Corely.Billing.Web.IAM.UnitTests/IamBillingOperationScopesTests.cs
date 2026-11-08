using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.IAM.UnitTests;

public class IamBillingOperationScopesTests
{
    private readonly Mock<IOperationScopes> _iamScopes = new();

    [Fact]
    public async Task RunAsync_RunsThroughIamsOperationScopes_ForWorkWithAResult()
    {
        Func<IServiceProvider, Task<int>> work = _ => Task.FromResult(7);
        _iamScopes.Setup(s => s.RunAsync(work)).ReturnsAsync(7);

        var result = await new IamBillingOperationScopes(_iamScopes.Object).RunAsync(work);

        Assert.Equal(7, result);
        _iamScopes.Verify(s => s.RunAsync(work), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReportsIamsAnswer_ForAScopeThatCouldNotSignIn()
    {
        Func<IServiceProvider, Task> work = _ => Task.CompletedTask;
        _iamScopes.Setup(s => s.RunAsync(work)).ReturnsAsync(false);

        Assert.False(await new IamBillingOperationScopes(_iamScopes.Object).RunAsync(work));
    }

    [Fact]
    public void AddBillingWebIam_RegistersIamsOperationScopes_ForBillingComponents()
    {
        var services = new ServiceCollection().AddBillingWebIam();

        var descriptor = services.Last(d => d.ServiceType == typeof(IBillingOperationScopes));

        Assert.Equal(typeof(IamBillingOperationScopes), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
