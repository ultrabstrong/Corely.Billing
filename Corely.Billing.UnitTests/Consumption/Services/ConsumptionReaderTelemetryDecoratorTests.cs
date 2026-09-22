using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Consumption.Services;

public class ConsumptionReaderTelemetryDecoratorTests
{
    private readonly Mock<IConsumptionReader> _mockInner = new();
    private readonly Mock<ILogger<ConsumptionReaderTelemetryDecorator>> _mockLogger = new();
    private readonly Mock<IBillingTelemetry> _mockTelemetry = new();
    private readonly ConsumptionReaderTelemetryDecorator _decorator;

    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestGrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public ConsumptionReaderTelemetryDecoratorTests()
    {
        _decorator = new ConsumptionReaderTelemetryDecorator(
            _mockInner.Object,
            _mockLogger.Object,
            _mockTelemetry.Object
        );
    }

    #region GetConsumptionTotalAsync Tests

    [Fact]
    public async Task GetConsumptionTotalAsync_DelegatesToInner()
    {
        var expected = new GetConsumptionTotalResult(
            GetConsumptionTotalResultCode.Success,
            null,
            42L
        );
        _mockInner
            .Setup(s =>
                s.GetConsumptionTotalAsync(
                    TestAccountId,
                    TestUsage.Page,
                    TestUsage.Extraction,
                    null,
                    null,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        var result = await _decorator.GetConsumptionTotalAsync(
            TestAccountId,
            TestUsage.Page,
            TestUsage.Extraction
        );

        Assert.Equal(GetConsumptionTotalResultCode.Success, result.ResultCode);
        Assert.Equal(42L, result.Total);
        _mockInner.Verify(
            s =>
                s.GetConsumptionTotalAsync(
                    TestAccountId,
                    TestUsage.Page,
                    TestUsage.Extraction,
                    null,
                    null,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetConsumptionTotalAsync_LogsException_WhenInnerThrows()
    {
        _mockInner
            .Setup(s =>
                s.GetConsumptionTotalAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<UsageUnit>(),
                    It.IsAny<UsageOperation>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<string?>(),
                    It.IsAny<IReadOnlyDictionary<string, string>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("test error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.GetConsumptionTotalAsync(TestAccountId, TestUsage.Page, TestUsage.Extraction)
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

    #region GetGrantConsumptionTotalsAsync Tests

    [Fact]
    public async Task GetGrantConsumptionTotalsAsync_DelegatesToInner()
    {
        var grantIds = new[] { TestGrantId };
        var totals = new List<GrantTotalConsumptions>
        {
            new GrantTotalConsumptions(TestGrantId, 10),
        };
        var expected = new GetGrantConsumptionTotalsResult(
            GetGrantConsumptionTotalsResultCode.Success,
            null,
            totals
        );
        _mockInner
            .Setup(s =>
                s.GetGrantConsumptionTotalsAsync(
                    TestAccountId,
                    grantIds,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        var result = await _decorator.GetGrantConsumptionTotalsAsync(TestAccountId, grantIds);

        Assert.Equal(GetGrantConsumptionTotalsResultCode.Success, result.ResultCode);
        Assert.Same(totals, result.Totals);
        _mockInner.Verify(
            s =>
                s.GetGrantConsumptionTotalsAsync(
                    TestAccountId,
                    grantIds,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    #endregion
}
