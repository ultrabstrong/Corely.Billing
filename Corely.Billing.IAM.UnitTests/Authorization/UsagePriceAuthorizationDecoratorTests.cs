using Corely.Billing.IAM.Authorization;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Accounts.Models;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class UsagePriceAuthorizationDecoratorTests
{
    private static readonly UsagePrice Price = new(
        UsageOperation.From("credits"),
        Money.Usd(0.001m)
    );

    private readonly Mock<IUsagePriceService> _inner = new();
    private readonly Mock<IAuthorizationProvider> _authorization = new();
    private readonly Mock<IUserContextProvider> _userContext = new();

    public UsagePriceAuthorizationDecoratorTests()
    {
        _inner
            .Setup(s => s.SetPriceAsync(It.IsAny<UsagePrice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChangeUsagePriceResult(ChangeUsagePriceResultCode.Success, ""));
        _authorization
            .Setup(p =>
                p.IsAuthorizedAsync(
                    AuthAction.Update,
                    BillingResourceTypes.USAGE_PRICE_RESOURCE_TYPE,
                    It.IsAny<Guid[]>()
                )
            )
            .ReturnsAsync(true);
    }

    private UsagePriceAuthorizationDecorator Decorator() =>
        new(_inner.Object, _authorization.Object, _userContext.Object);

    private void SignedInto(bool platformAccount) =>
        _userContext
            .Setup(p => p.GetUserContext())
            .Returns(
                new UserContext(
                    new User { Id = Guid.CreateVersion7(), Username = "owner" },
                    new Account
                    {
                        Id = Guid.CreateVersion7(),
                        AccountName = "Account",
                        IsPlatformAccount = platformAccount,
                    },
                    "device",
                    []
                )
            );

    [Fact]
    public async Task SetPriceAsync_Changes_ForAPermittedUserInThePlatformAccount()
    {
        SignedInto(platformAccount: true);

        var result = await Decorator().SetPriceAsync(Price);

        Assert.Equal(ChangeUsagePriceResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task SetPriceAsync_IsRefused_ForAPermittedUserInAnotherAccount()
    {
        SignedInto(platformAccount: false);

        var result = await Decorator().SetPriceAsync(Price);

        Assert.Equal(ChangeUsagePriceResultCode.UnauthorizedError, result.ResultCode);
        _inner.Verify(
            s => s.SetPriceAsync(It.IsAny<UsagePrice>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task SetPriceAsync_Changes_ForTheSystemContext()
    {
        _userContext.Setup(p => p.GetUserContext()).Returns(new UserContext(true, "device"));

        var result = await Decorator().SetPriceAsync(Price);

        Assert.Equal(ChangeUsagePriceResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task GetPriceAsync_Reads_ForAnyCaller()
    {
        _inner
            .Setup(s => s.GetPriceAsync(Price.Operation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Price.UnitPrice);

        Assert.Equal(Price.UnitPrice, await Decorator().GetPriceAsync(Price.Operation));
    }
}
