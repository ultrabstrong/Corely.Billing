using Corely.Billing.Resilience;

namespace Corely.Billing.UnitTests.Resilience;

public class RetryOptionsTests
{
    private static readonly RetryOptions NoJitter = new()
    {
        BaseDelay = TimeSpan.FromMilliseconds(100),
        BackoffFactor = 2,
        MaxDelay = TimeSpan.FromMilliseconds(1_000),
        JitterFactor = 0,
    };

    [Fact]
    public void IsRetryable_RetriesEverything_ForNoPredicate() =>
        Assert.True(new RetryOptions().IsRetryable(new InvalidOperationException()));

    [Fact]
    public void IsRetryable_AsksThePredicate_ForAPredicate()
    {
        var options = new RetryOptions { ShouldRetry = ex => ex is TimeoutException };

        Assert.True(options.IsRetryable(new TimeoutException()));
        Assert.False(options.IsRetryable(new InvalidOperationException()));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 200)]
    [InlineData(3, 400)]
    [InlineData(4, 800)]
    [InlineData(5, 1_000)]
    [InlineData(20, 1_000)]
    public void RetryDelay_DoublesUpToTheCap_ForEachAttempt(int attempt, double expectedMs) =>
        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMs),
            NoJitter.RetryDelay(attempt, new Random(1))
        );

    [Fact]
    public void RetryDelay_StaysWithinTheJitterBand_ForManyDraws()
    {
        var options = NoJitter with { JitterFactor = 0.1 };
        var random = new Random(7);

        var delays = Enumerable.Range(0, 500).Select(_ => options.RetryDelay(2, random)).ToList();

        Assert.All(delays, d => Assert.InRange(d.TotalMilliseconds, 180, 220));
        Assert.True(delays.Distinct().Count() > 1);
    }
}
