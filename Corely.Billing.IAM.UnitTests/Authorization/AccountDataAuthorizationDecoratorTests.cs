using Corely.Billing.Accounts.Models;
using Corely.Billing.IAM.Authorization;
using Corely.Billing.Services;
using Corely.IAM.Accounts.Models;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class AccountDataAuthorizationDecoratorTests
{
    private static readonly Guid AccountId = Guid.CreateVersion7();

    private readonly Mock<IAccountDataService> _inner = new();
    private readonly Mock<IUserContextProvider> _userContext = new();

    public AccountDataAuthorizationDecoratorTests()
    {
        _inner
            .Setup(s => s.DeleteAccountDataAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteAccountDataResult(DeleteAccountDataResultCode.Success, ""));
    }

    private AccountDataAuthorizationDecorator Decorator() =>
        new(_inner.Object, _userContext.Object);

    [Fact]
    public async Task DeleteAccountDataAsync_Deletes_ForTheSystemContext()
    {
        _userContext
            .Setup(p => p.GetUserContext())
            .Returns(new UserContext(true, "account-deletion"));

        var result = await Decorator().DeleteAccountDataAsync(AccountId);

        Assert.Equal(DeleteAccountDataResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task DeleteAccountDataAsync_RefusesWithoutDeleting_ForTheAccountsOwnUser()
    {
        _userContext
            .Setup(p => p.GetUserContext())
            .Returns(
                new UserContext(
                    new User { Id = Guid.CreateVersion7(), Username = "owner" },
                    new Account { Id = AccountId, AccountName = "Account" },
                    "device",
                    []
                )
            );

        var result = await Decorator().DeleteAccountDataAsync(AccountId);

        Assert.Equal(DeleteAccountDataResultCode.UnauthorizedError, result.ResultCode);
        _inner.Verify(
            s => s.DeleteAccountDataAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
