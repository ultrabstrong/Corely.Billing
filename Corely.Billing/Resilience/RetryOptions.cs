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

    public bool IsRetryable(Exception ex) => ShouldRetry?.Invoke(ex) ?? true;

    public TimeSpan RetryDelay(int attempt, Random random)
    {
        var exponential = BaseDelay.TotalMilliseconds * Math.Pow(BackoffFactor, attempt - 1);
        var ms = Math.Min(exponential, MaxDelay.TotalMilliseconds);
        if (JitterFactor > 0)
            ms *= 1 + ((random.NextDouble() * 2 - 1) * JitterFactor);
        return TimeSpan.FromMilliseconds(ms);
    }
}
