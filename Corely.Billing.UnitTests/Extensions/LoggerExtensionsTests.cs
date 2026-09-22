using Corely.Billing.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Extensions;

public class LoggerExtensionsTests
{
    private readonly Mock<ILogger> _mockLogger = new();

    private void VerifyLogged(LogLevel level, Func<string, bool> matches, Times times) =>
        _mockLogger.Verify(
            x =>
                x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => matches(v.ToString()!)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times
        );

    [Fact]
    public async Task ExecuteWithLoggingAsync_LogsEntryAndExit_ForASuccessfulOperation()
    {
        var actualResult = await _mockLogger.Object.ExecuteWithLoggingAsync(
            "TestClass",
            "test-request",
            () => Task.FromResult("test-result")
        );

        Assert.Equal("test-result", actualResult);
        VerifyLogged(LogLevel.Trace, m => m.Contains("starting with request"), Times.Once());
        VerifyLogged(
            LogLevel.Trace,
            m => m.Contains("completed") && !m.Contains("with result"),
            Times.Once()
        );
    }

    [Fact]
    public async Task ExecuteWithLoggingAsync_LogsTheResult_ForLogResultTrue()
    {
        await _mockLogger.Object.ExecuteWithLoggingAsync(
            "TestClass",
            "test-request",
            () => Task.FromResult("test-result"),
            logResult: true
        );

        VerifyLogged(LogLevel.Trace, m => m.Contains("with result"), Times.Once());
    }

    [Fact]
    public async Task ExecuteWithLoggingAsync_LogsAndRethrows_ForAnOperationThatThrows()
    {
        var expected = new InvalidOperationException("Test exception");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _mockLogger.Object.ExecuteWithLoggingAsync(
                "TestClass",
                "test-request",
                () => Task.FromException<string>(expected)
            )
        );

        Assert.Same(expected, ex);
        VerifyLogged(LogLevel.Error, m => m.Contains("failed"), Times.Once());
    }

    [Fact]
    public async Task ExecuteWithLoggingAsync_Throws_ForANullRequest() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _mockLogger.Object.ExecuteWithLoggingAsync<string?, string>(
                "TestClass",
                null,
                () => Task.FromResult("result")
            )
        );

    [Fact]
    public async Task ExecuteWithLoggingAsync_NamesTheCaller_ForAnyCall()
    {
        await CallerAsync();

        VerifyLogged(LogLevel.Trace, m => m.Contains(nameof(CallerAsync)), Times.AtLeastOnce());
    }

    private Task<string> CallerAsync() =>
        _mockLogger.Object.ExecuteWithLoggingAsync(
            "TestClass",
            "test-request",
            () => Task.FromResult("test-result")
        );
}
