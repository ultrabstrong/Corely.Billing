using Corely.Billing.Consumption.Models;
using Corely.Billing.IAM.Authorization;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class ConsumptionAuthorizationDecoratorTests
{
    private const string CONSUMPTION = BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE;

    private readonly Mock<IConsumptionService> _inner = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IAuthorizationProvider> _authorization = new();

    public ConsumptionAuthorizationDecoratorTests()
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(true);
        _authorization
            .Setup(p => p.IsAuthorizedAsync(AuthAction.Read, CONSUMPTION, It.IsAny<Guid[]>()))
            .ReturnsAsync(true);
        _inner
            .Setup(s => s.ListProvidersAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RetrieveSingleResult<List<string>>(RetrieveResultCode.Success, "", ["prov"])
            );
        _inner
            .Setup(s =>
                s.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<ConsumptionEvent>(
                    RetrieveResultCode.Success,
                    "",
                    PagedResult<ConsumptionEvent>.Empty()
                )
            );
    }

    private ConsumptionAuthorizationDecorator Decorator() =>
        new(_inner.Object, _authorization.Object);

    public static TheoryData<string> Reads =>
        ["total", "grant totals", "time series", "events", "providers", "earliest", "abandoned"];

    private async Task<RetrieveResultCode> ReadAsync(string read)
    {
        var decorator = Decorator();
        var now = DateTime.UtcNow;
        return read switch
        {
            "total" => (
                await decorator.GetConsumptionTotalAsync(new(AccountId, Extraction, Page))
            ).ResultCode,
            "grant totals" => (
                await decorator.GetGrantConsumptionTotalsAsync(AccountId, [GrantId])
            ).ResultCode,
            "time series" => (
                await decorator.GetConsumptionTimeSeriesAsync(
                    new(AccountId, now.AddDays(-1), now, TimeBucket.Day)
                )
            ).ResultCode,
            "events" => (await decorator.ListConsumptionEventsAsync(new(AccountId))).ResultCode,
            "providers" => (await decorator.ListProvidersAsync(AccountId)).ResultCode,
            "earliest" => (await decorator.GetEarliestConsumptionAsync(AccountId)).ResultCode,
            _ => (await decorator.CountAbandonedReservationsAsync(AccountId)).ResultCode,
        };
    }

    [Theory]
    [MemberData(nameof(Reads))]
    public async Task EveryRead_ReturnsUnauthorized_ForNoReadOnConsumption(string read)
    {
        _authorization
            .Setup(p => p.IsAuthorizedAsync(AuthAction.Read, CONSUMPTION, It.IsAny<Guid[]>()))
            .ReturnsAsync(false);

        Assert.Equal(RetrieveResultCode.UnauthorizedError, await ReadAsync(read));
        Assert.Empty(_inner.Invocations);
    }

    [Theory]
    [MemberData(nameof(Reads))]
    public async Task EveryRead_ReturnsUnauthorized_ForAnotherAccount(string read)
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(false);

        Assert.Equal(RetrieveResultCode.UnauthorizedError, await ReadAsync(read));
        Assert.Empty(_inner.Invocations);
    }

    [Fact]
    public async Task ListProvidersAsync_CallsInner_ForReadOnConsumptionInTheAccount()
    {
        var result = await Decorator().ListProvidersAsync(AccountId);

        Assert.Equal(["prov"], result.Item);
        _authorization.Verify(p => p.IsAuthorizedAsync(AuthAction.Read, CONSUMPTION), Times.Once);
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_CallsInner_ForReadOnConsumptionInTheAccount()
    {
        var result = await Decorator().ListConsumptionEventsAsync(new(AccountId));

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
    }
}
