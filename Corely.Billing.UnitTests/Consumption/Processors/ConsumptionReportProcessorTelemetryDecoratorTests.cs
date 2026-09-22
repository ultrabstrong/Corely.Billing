using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Models;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Consumption.Processors;

public class ConsumptionReportProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IConsumptionReportProcessor> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();
    private readonly ConsumptionReportProcessorTelemetryDecorator _decorator;

    public ConsumptionReportProcessorTelemetryDecoratorTests()
    {
        _decorator = new ConsumptionReportProcessorTelemetryDecorator(
            _inner.Object,
            Mock.Of<ILogger<ConsumptionReportProcessorTelemetryDecorator>>(),
            _telemetry.Object
        );
    }

    [Fact]
    public async Task GetConsumptionTotalAsync_RecordsTheTotal_ForAnyTotal()
    {
        var request = new GetConsumptionTotalRequest(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page
        );
        _inner.Setup(p => p.GetConsumptionTotalAsync(request, default)).ReturnsAsync(42);

        var total = await _decorator.GetConsumptionTotalAsync(request);

        Assert.Equal(42, total);
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.CONSUMPTION_TOTAL_QUERIED, 42),
            Times.Once
        );
    }

    [Fact]
    public async Task GetGrantConsumptionTotalsAsync_RecordsTheGrantCount_ForTheRequestedGrants()
    {
        Guid[] grantIds = [GrantId, Guid.CreateVersion7()];
        List<GrantTotalConsumptions> totals = [new(GrantId, 10)];
        _inner
            .Setup(p => p.GetGrantConsumptionTotalsAsync(AccountId, grantIds, default))
            .ReturnsAsync(totals);

        var result = await _decorator.GetGrantConsumptionTotalsAsync(AccountId, grantIds);

        Assert.Same(totals, result);
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.CONSUMPTION_GRANTS_QUERIED, 2),
            Times.Once
        );
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_RecordsTheTotalCount_ForAPage()
    {
        var request = new ListConsumptionEventsRequest(AccountId);
        _inner
            .Setup(p => p.ListConsumptionEventsAsync(request, default))
            .ReturnsAsync(PagedResult<ConsumptionEvent>.Create([], 13, 0, 25));

        await _decorator.ListConsumptionEventsAsync(request);

        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.CONSUMPTION_EVENTS_LISTED, 13),
            Times.Once
        );
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_RecordsTheCount_ForZeroAbandoned()
    {
        _inner.Setup(p => p.CountAbandonedReservationsAsync(AccountId, default)).ReturnsAsync(0);

        await _decorator.CountAbandonedReservationsAsync(AccountId);

        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.RESERVATION_ABANDONED, 0),
            Times.Once
        );
    }
}
