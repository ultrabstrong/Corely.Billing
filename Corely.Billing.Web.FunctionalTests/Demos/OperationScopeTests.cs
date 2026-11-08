extern alias PortalDemo;

using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.Extensions.DependencyInjection;
using PortalDemo::Corely.Billing.Demos.Portal.Components;

namespace Corely.Billing.Web.FunctionalTests.Demos;

// A Blazor tab is one DI scope that lives as long as the circuit. This opens one on the Portal
// demo's real registrations and reads through its operations, as GrantList does.
public sealed class OperationScopeTests : IDisposable
{
    // The Portal demo's account and an operation its vocabulary knows (DemoUsage is internal to it).
    private static readonly Guid AccountId = Guid.Parse("0199a0de-0000-7000-8000-00000000d3e0");
    private static readonly UsageOperation TextGeneration = UsageOperation.From("text_generation");

    private readonly DemoAppFactory<App> _factory = new();

    public OperationScopeTests() => _factory.CreateBillingSchema();

    public void Dispose() => _factory.Dispose();

    private Task<GrantRequestStatus?> StatusAsync(IBillingOperationScopes tab, Guid id) =>
        tab.RunAsync(async services =>
        {
            var result = await services
                .GetRequiredService<IGrantRequestService>()
                .ListGrantRequestsAsync(
                    new ListGrantRequestsRequest([AccountId], Take: int.MaxValue)
                );
            Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
            return (GrantRequestStatus?)
                result.Data!.Items.Single(r => r.GrantRequestId == id).Status;
        });

    private async Task<T> InAnotherScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    [Fact]
    public async Task RunAsync_ReadsTheCancelledRequest_ForAReloadAfterAnotherScopeCancelledIt()
    {
        var requested = await InAnotherScopeAsync(services =>
            services
                .GetRequiredService<IGrantRequestService>()
                .RequestGrantAsync(
                    new RequestGrantRequest(AccountId, TextGeneration, 1_000, null, null)
                )
        );
        Assert.Equal(RequestGrantResultCode.Success, requested.ResultCode);
        await using var tabScope = _factory.Services.CreateAsyncScope();
        var tab = tabScope.ServiceProvider.GetRequiredService<IBillingOperationScopes>();
        Assert.Equal(GrantRequestStatus.Pending, await StatusAsync(tab, requested.CreatedId));

        var cancelled = await InAnotherScopeAsync(services =>
            services
                .GetRequiredService<IGrantRequestService>()
                .CancelGrantRequestAsync(
                    new CancelGrantRequestRequest(
                        AccountId,
                        requested.CreatedId,
                        "No longer needed",
                        null
                    )
                )
        );

        Assert.Equal(DecideGrantRequestResultCode.Success, cancelled.ResultCode);
        Assert.Equal(GrantRequestStatus.Cancelled, await StatusAsync(tab, requested.CreatedId));
    }
}
