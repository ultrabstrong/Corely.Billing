using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Consumption.Services;

public class ConsumptionWriterTelemetryDecoratorTests
{
    private readonly Mock<IConsumptionWriter> _mockInner = new();
    private readonly Mock<ILogger<ConsumptionWriterTelemetryDecorator>> _mockLogger = new();
    private readonly Mock<IBillingTelemetry> _mockTelemetry = new();
    private readonly ConsumptionWriterTelemetryDecorator _decorator;

    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ConsumptionEvent MakeEvent()
    {
        var createResult = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: 1,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            provider: "mistral",
            utcTimestamp: DateTime.UtcNow,
            grantId: Guid.CreateVersion7()
        );
        Assert.True(createResult.IsSuccess);
        return createResult.Value;
    }

    public ConsumptionWriterTelemetryDecoratorTests()
    {
        _decorator = new ConsumptionWriterTelemetryDecorator(
            _mockInner.Object,
            _mockLogger.Object,
            _mockTelemetry.Object
        );
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_DelegatesToInner()
    {
        var evt = MakeEvent();
        _mockInner
            .Setup(s => s.SaveAsync(evt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveConsumptionResult(SaveConsumptionResultCode.Success, null));

        var result = await _decorator.SaveAsync(evt);

        Assert.Equal(SaveConsumptionResultCode.Success, result.ResultCode);
        _mockInner.Verify(s => s.SaveAsync(evt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_LogsException_WhenInnerThrows()
    {
        _mockInner
            .Setup(s => s.SaveAsync(It.IsAny<ConsumptionEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.SaveAsync(MakeEvent())
        );

        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    #endregion
}
