using Corely.Billing.IAM.Authorization;
using Corely.IAM.Accounts.Models;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class AccountReadScopeTests
{
    private const string GRANT = BillingResourceTypes.GRANT_RESOURCE_TYPE;
    private const string ALL_ACCOUNTS = BillingResourceTypes.ALL_ACCOUNTS_RESOURCE_TYPE;

    private static readonly Guid OtherAccountId = Guid.CreateVersion7();

    private readonly Mock<IAuthorizationProvider> _authorization = new();
    private UserContext? _context;

    public AccountReadScopeTests() =>
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(true);

    private AccountReadScope Scope() =>
        new(
            _authorization.Object,
            Mock.Of<IUserContextProvider>(p => p.GetUserContext() == _context)
        );

    private void Allow(params string[] resources) =>
        _authorization
            .Setup(p =>
                p.IsAuthorizedAsync(AuthAction.Read, It.IsAny<string>(), It.IsAny<Guid[]>())
            )
            .ReturnsAsync((AuthAction _, string r, Guid[] _) => resources.Contains(r));

    private void SignInTo(bool platformAccount) =>
        _context = new UserContext(
            new User { Id = Guid.CreateVersion7(), Username = "admin" },
            new Account
            {
                Id = AccountId,
                AccountName = "Acme",
                IsPlatformAccount = platformAccount,
            },
            "device",
            []
        );

    [Fact]
    public async Task CanReadAsync_Allows_ForTheCurrentAccountWithRead()
    {
        Allow(GRANT);

        Assert.True(await Scope().CanReadAsync([AccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Refuses_ForTheCurrentAccountWithoutRead()
    {
        Allow();

        Assert.False(await Scope().CanReadAsync([AccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Refuses_ForSeveralAccountsWithoutTheAllAccountsPermission()
    {
        SignInTo(platformAccount: true);
        Allow(GRANT);

        Assert.False(await Scope().CanReadAsync([AccountId, OtherAccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Allows_ForSeveralAccountsWithTheAllAccountsPermissionInThePlatformAccount()
    {
        SignInTo(platformAccount: true);
        Allow(GRANT, ALL_ACCOUNTS);

        Assert.True(await Scope().CanReadAsync([AccountId, OtherAccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Refuses_ForTheAllAccountsPermissionGivenInAnOrdinaryAccount()
    {
        SignInTo(platformAccount: false);
        Allow(GRANT, ALL_ACCOUNTS);

        Assert.False(await Scope().CanReadAsync([AccountId, OtherAccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Refuses_ForAnotherSingleAccountWithoutTheAllAccountsPermission()
    {
        SignInTo(platformAccount: true);
        Allow(GRANT);

        Assert.False(await Scope().CanReadAsync([OtherAccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Allows_ForTheSystemContext()
    {
        _context = new UserContext(true, "functions");

        Assert.True(await Scope().CanReadAsync([AccountId, OtherAccountId], GRANT));
    }

    [Fact]
    public async Task CanReadAsync_Refuses_ForNoAccounts() =>
        Assert.False(await Scope().CanReadAsync([], GRANT));
}
