namespace Corely.Billing.Resilience;

internal sealed record RetryOptions
{
    public int MaxAttempts { get; init; } = 3;

    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public double BackoffFactor { get; init; } = 2.0;

    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);

    public double JitterFactor { get; init; } = 0.1;

    public bool FastFirst { get; init; } = true;

    public Func<Exception, bool>? ShouldRetry { get; init; }

    public Action<int, Exception, TimeSpan>? OnRetry { get; init; }
}
