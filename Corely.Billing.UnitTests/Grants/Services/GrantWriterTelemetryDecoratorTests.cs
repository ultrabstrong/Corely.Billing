using Corely.Billing;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Services;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Grants.Services;

public class GrantWriterTelemetryDecoratorTests
{
    private readonly Mock<IGrantWriter> _mockInner = new();
    private readonly Mock<ILogger<GrantWriterTelemetryDecorator>> _mockLogger = new();
    private readonly Mock<IBillingTelemetry> _mockTelemetry = new();
    private readonly GrantWriterTelemetryDecorator _decorator;

    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestGrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Grant MakeGrant()
    {
        var createResult = Grant.Create(
            accountId: TestAccountId,
            quantity: 100,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            validFromUtc: DateTime.UtcNow,
            validToUtc: DateTime.UtcNow.AddMonths(1),
            grantId: TestGrantId
        );
        Assert.True(createResult.IsSuccess);
        return createResult.Value;
    }

    public GrantWriterTelemetryDecoratorTests()
    {
        _decorator = new GrantWriterTelemetryDecorator(
            _mockInner.Object,
            _mockLogger.Object,
            _mockTelemetry.Object
        );
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_DelegatesToInner()
    {
        var grant = MakeGrant();
        _mockInner
            .Setup(s => s.SaveAsync(grant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SaveGrantResult(SaveGrantResultCode.Success, null));

        var result = await _decorator.SaveAsync(grant);

        Assert.Equal(SaveGrantResultCode.Success, result.ResultCode);
        _mockInner.Verify(s => s.SaveAsync(grant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_LogsException_WhenInnerThrows()
    {
        _mockInner
            .Setup(s => s.SaveAsync(It.IsAny<Grant>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.SaveAsync(MakeGrant())
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

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_DelegatesToInner()
    {
        var grant = MakeGrant();
        _mockInner
            .Setup(s => s.UpdateAsync(grant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateGrantResult(UpdateGrantResultCode.Success, null));

        var result = await _decorator.UpdateAsync(grant);

        Assert.Equal(UpdateGrantResultCode.Success, result.ResultCode);
        _mockInner.Verify(s => s.UpdateAsync(grant, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_DelegatesToInner()
    {
        _mockInner
            .Setup(s => s.DeleteAsync(TestAccountId, TestGrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.Success, null));

        var result = await _decorator.DeleteAsync(TestAccountId, TestGrantId);

        Assert.Equal(DeleteGrantResultCode.Success, result.ResultCode);
        _mockInner.Verify(
            s => s.DeleteAsync(TestAccountId, TestGrantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion
}
