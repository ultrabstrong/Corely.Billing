using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Accounts.Models;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.Web.IAM.UnitTests.Extensions;

public class UserContextProviderExtensionsTests
{
    [Fact]
    public void CurrentAccountId_IsTheContextsAccount_ForASignedInCaller()
    {
        var account = new Account { Id = Guid.CreateVersion7(), AccountName = "Acme" };
        var provider = new Mock<IUserContextProvider>();
        provider
            .Setup(p => p.GetUserContext())
            .Returns(
                new UserContext(new User { Id = Guid.CreateVersion7() }, account, "d", [account])
            );

        Assert.Equal(account.Id, provider.Object.CurrentAccountId);
    }

    [Fact]
    public void CurrentAccountId_IsNull_ForAnAnonymousCaller() =>
        Assert.Null(new Mock<IUserContextProvider>().Object.CurrentAccountId);
}
