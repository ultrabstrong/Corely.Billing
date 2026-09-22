using Corely.Billing;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Services;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Grants.Services;

public class GrantReaderTelemetryDecoratorTests
{
    private readonly Mock<IGrantReader> _mockInner = new();
    private readonly Mock<ILogger<GrantReaderTelemetryDecorator>> _mockLogger = new();
    private readonly Mock<IBillingTelemetry> _mockTelemetry = new();
    private readonly GrantReaderTelemetryDecorator _decorator;

    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestGrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public GrantReaderTelemetryDecoratorTests()
    {
        _decorator = new GrantReaderTelemetryDecorator(
            _mockInner.Object,
            _mockLogger.Object,
            _mockTelemetry.Object
        );
    }

    #region GetAllGrantsAsync Tests

    [Fact]
    public async Task GetAllGrantsAsync_DelegatesToInner()
    {
        var grants = new List<Grant>();
        var expected = new GetAllGrantsResult(GetAllGrantsResultCode.Success, null, grants);
        _mockInner
            .Setup(s => s.GetAllGrantsAsync(TestAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _decorator.GetAllGrantsAsync(TestAccountId);

        Assert.Equal(GetAllGrantsResultCode.Success, result.ResultCode);
        Assert.Same(grants, result.Grants);
        _mockInner.Verify(
            s => s.GetAllGrantsAsync(TestAccountId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAllGrantsAsync_LogsException_WhenInnerThrows()
    {
        _mockInner
            .Setup(s => s.GetAllGrantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.GetAllGrantsAsync(TestAccountId)
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

    #region GetGrantByIdAsync Tests

    [Fact]
    public async Task GetGrantByIdAsync_DelegatesToInner()
    {
        var expected = new GetGrantByIdResult(GetGrantByIdResultCode.NotFound, null);
        _mockInner
            .Setup(s =>
                s.GetGrantByIdAsync(TestAccountId, TestGrantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expected);

        var result = await _decorator.GetGrantByIdAsync(TestAccountId, TestGrantId);

        Assert.Equal(GetGrantByIdResultCode.NotFound, result.ResultCode);
        _mockInner.Verify(
            s => s.GetGrantByIdAsync(TestAccountId, TestGrantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    #endregion

    #region GetActiveGrantsAsync Tests

    [Fact]
    public async Task GetActiveGrantsAsync_DelegatesToInner()
    {
        var atUtc = DateTime.UtcNow;
        var grants = new List<Grant>();
        var expected = new GetActiveGrantsResult(GetActiveGrantsResultCode.Success, null, grants);
        _mockInner
            .Setup(s =>
                s.GetActiveGrantsAsync(
                    TestAccountId,
                    atUtc,
                    null,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        var result = await _decorator.GetActiveGrantsAsync(TestAccountId, atUtc);

        Assert.Equal(GetActiveGrantsResultCode.Success, result.ResultCode);
        Assert.Same(grants, result.Grants);
        _mockInner.Verify(
            s =>
                s.GetActiveGrantsAsync(
                    TestAccountId,
                    atUtc,
                    null,
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    #endregion
}
