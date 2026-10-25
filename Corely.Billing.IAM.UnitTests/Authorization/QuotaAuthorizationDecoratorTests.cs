using Corely.Billing.IAM.Authorization;
using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class QuotaAuthorizationDecoratorTests
{
    private const string QUOTA = BillingResourceTypes.QUOTA_RESOURCE_TYPE;

    private readonly Mock<IQuotaService> _inner = new();
    private readonly Mock<IAuthorizationProvider> _authorization = new();

    public QuotaAuthorizationDecoratorTests()
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(true);
        _inner
            .Setup(s =>
                s.GetBalanceAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new GetBalanceResult(QuotaReadResultCode.Success, "", 10, []));
        _inner
            .Setup(s =>
                s.ReserveAsync(It.IsAny<ReserveQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ReserveQuotaResult(ReserveQuotaResultCode.Success, ""));
        _inner
            .Setup(s =>
                s.SettleAsync(It.IsAny<SettleQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new SettleQuotaResult(SettleQuotaResultCode.Success, ""));
        _inner
            .Setup(s =>
                s.ReleaseAsync(It.IsAny<ReleaseQuotaRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new SettleQuotaResult(SettleQuotaResultCode.Success, ""));
        _inner
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResult(RegisterResultCode.Registered, ""));
    }

    private QuotaAuthorizationDecorator Decorator() =>
        new(
            _inner.Object,
            _authorization.Object,
            new AccountReadScope(_authorization.Object, Mock.Of<IUserContextProvider>())
        );

    private void AllowOnly(params AuthAction[] actions) =>
        _authorization
            .Setup(p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsAny<string>(), It.IsAny<Guid[]>())
            )
            .ReturnsAsync((AuthAction a, string r, Guid[] _) => r == QUOTA && actions.Contains(a));

    private Task<GetBalanceResult> BalanceAsync() =>
        Decorator().GetBalanceAsync(AccountId, Extraction);

    private Task<ReserveQuotaResult> ReserveAsync() =>
        Decorator().ReserveAsync(new ReserveQuotaRequest(AccountId, Extraction, Page, 1, "prov"));

    private Task<SettleQuotaResult> SettleAsync() =>
        Decorator().SettleAsync(new SettleQuotaRequest(AccountId, Extraction, Page, 5));

    private Task<SettleQuotaResult> ReleaseAsync() =>
        Decorator().ReleaseAsync(new ReleaseQuotaRequest(AccountId, Extraction, Page));

    [Fact]
    public async Task GetBalanceAsync_CallsInner_ForReadOnQuotaAlone()
    {
        AllowOnly(AuthAction.Read);

        Assert.Equal(QuotaReadResultCode.Success, (await BalanceAsync()).ResultCode);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsUnauthorized_ForExecuteWithoutRead()
    {
        AllowOnly(AuthAction.Execute);

        Assert.Equal(QuotaReadResultCode.UnauthorizedError, (await BalanceAsync()).ResultCode);
    }

    [Fact]
    public async Task GetBalanceAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        AllowOnly(AuthAction.Read);
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(false);

        Assert.Equal(QuotaReadResultCode.UnauthorizedError, (await BalanceAsync()).ResultCode);
    }

    [Fact]
    public async Task ReserveSettleAndRelease_CallInner_ForExecuteOnQuotaAlone()
    {
        AllowOnly(AuthAction.Execute);

        Assert.Equal(ReserveQuotaResultCode.Success, (await ReserveAsync()).ResultCode);
        Assert.Equal(SettleQuotaResultCode.Success, (await SettleAsync()).ResultCode);
        Assert.Equal(SettleQuotaResultCode.Success, (await ReleaseAsync()).ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsNotIn(QUOTA), It.IsAny<Guid[]>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ReserveSettleAndRelease_ReturnUnauthorized_ForReadWithoutExecute()
    {
        AllowOnly(AuthAction.Read);

        var reserve = await ReserveAsync();
        var settle = await SettleAsync();
        var release = await ReleaseAsync();

        Assert.Equal(
            (ReserveQuotaResultCode.UnauthorizedError, "Unauthorized to reserve quota"),
            (reserve.ResultCode, reserve.Message)
        );
        Assert.Equal(
            (SettleQuotaResultCode.UnauthorizedError, "Unauthorized to settle quota"),
            (settle.ResultCode, settle.Message)
        );
        Assert.Equal(
            (SettleQuotaResultCode.UnauthorizedError, "Unauthorized to release quota"),
            (release.ResultCode, release.Message)
        );
        _inner.Verify(
            s => s.ReserveAsync(It.IsAny<ReserveQuotaRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ReserveAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        AllowOnly(AuthAction.Execute);
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(false);

        Assert.Equal(ReserveQuotaResultCode.UnauthorizedError, (await ReserveAsync()).ResultCode);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsUnauthorizedWithoutCallingInner_ForReadWithoutExecute()
    {
        AllowOnly(AuthAction.Read);

        var result = await Decorator().RegisterAsync(new RegisterRequest(AccountId, Extraction));

        Assert.Equal(RegisterResultCode.UnauthorizedError, result.ResultCode);
        _inner.Verify(
            s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ReleaseRegistrationAsync_ReturnsUnauthorized_ForReadWithoutExecute()
    {
        AllowOnly(AuthAction.Read);

        var result = await Decorator().ReleaseRegistrationAsync(AccountId, Guid.CreateVersion7());

        Assert.Equal(ModifyResultCode.UnauthorizedError, result.ResultCode);
    }
}
