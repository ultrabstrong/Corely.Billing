using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Consumption.Processors;

public class ConsumptionProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<IConsumptionProcessor> _inner = new();
    private readonly Mock<ILogger<ConsumptionProcessorTelemetryDecorator>> _logger = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();
    private readonly ConsumptionProcessorTelemetryDecorator _decorator;

    public ConsumptionProcessorTelemetryDecoratorTests()
    {
        _decorator = new ConsumptionProcessorTelemetryDecorator(
            _inner.Object,
            _logger.Object,
            _telemetry.Object
        );
    }

    private static ConsumptionEvent MakeEvent() =>
        new()
        {
            AccountId = AccountId,
            GrantId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 6,
            Provider = "mistral",
        };

    [Fact]
    public async Task ReserveAsync_RecordsTheReservation_ForASuccessfulReserve()
    {
        _inner
            .Setup(p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), default))
            .ReturnsAsync(new ReserveConsumptionResult(ReserveConsumptionResultCode.Success, ""));

        await _decorator.ReserveAsync(MakeEvent());

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Consumption.RESERVATION_TAKEN),
            Times.Once
        );
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.RESERVATION_QUANTITY, 6),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_RecordsNothing_ForAFailedReserve()
    {
        _inner
            .Setup(p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), default))
            .ReturnsAsync(
                new ReserveConsumptionResult(ReserveConsumptionResultCode.NotRecordedError, "no")
            );

        await _decorator.ReserveAsync(MakeEvent());

        _telemetry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SettleAsync_RecordsTheSettledQuantity_ForASuccessfulSettle()
    {
        var split = new Dictionary<Guid, long>();
        _inner
            .Setup(p =>
                p.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split, default)
            )
            .ReturnsAsync(
                new ResolveConsumptionResult(ResolveConsumptionResultCode.Success, "", 9, 2)
            );

        await _decorator.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Consumption.RESERVATION_SETTLED),
            Times.Once
        );
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Consumption.CONSUMPTION_QUANTITY, 9),
            Times.Once
        );
    }

    [Fact]
    public async Task ReleaseAsync_RecordsTheRelease_ForASuccessfulRelease()
    {
        _inner
            .Setup(p => p.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page, default))
            .ReturnsAsync(new ResolveConsumptionResult(ResolveConsumptionResultCode.Success, ""));

        await _decorator.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Consumption.RESERVATION_RELEASED),
            Times.Once
        );
    }

    [Fact]
    public async Task ReserveAsync_LogsAndRethrows_ForAFaultInTheInnerProcessor()
    {
        _inner
            .Setup(p => p.ReserveAsync(It.IsAny<ConsumptionEvent>(), default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.ReserveAsync(MakeEvent())
        );

        _logger.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
        _telemetry.VerifyNoOtherCalls();
    }
}
