using Corely.Billing.Grants.Models;
using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.UnitTests.Components;

public class BillingComponentBaseTests : BillingWebTestContext
{
    [Fact]
    public void ParametersSet_WaitsForTheScopeToSignIn_ForParametersSentAgainDuringSignIn()
    {
        var signIn = new TaskCompletionSource<Guid?>();
        Services.AddSingleton(
            Mock.Of<IBillingAccountAccessor>(a => a.GetAccountIdAsync() == signIn.Task)
        );

        var list = Render<GrantList>(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.NewGrantHref, "/new")
                .Add(c => c.RequestGrantHref, "/request")
                .Add(c => c.GrantHref, id => $"/{id}")
        );
        list.Render(p => p.Add(c => c.NewGrantHref, "/new-again"));

        Grants.Verify(
            g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );

        signIn.SetResult(AccountId);

        list.WaitForAssertion(() =>
            Grants.Verify(
                g =>
                    g.ListGrantsAsync(
                        It.IsAny<ListGrantsRequest>(),
                        It.IsAny<IReadOnlySet<Guid>?>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Once
            )
        );
    }
}
