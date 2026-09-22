namespace Corely.Billing;

internal sealed record RetryOptions
{
    /// <summary>
    /// Total number of attempts including the initial try.
    /// For example: MaxAttempts = 3 allows the initial execution plus up to two retries.
    /// Must be greater than or equal to 1.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Base delay used to compute backoff between retries.
    /// The effective delay is derived from this value and <see cref="BackoffFactor"/>.
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Exponential multiplier applied per retry attempt.
    /// For attempt N (starting at 1), delay ≈ BaseDelay * BackoffFactor^(N - 1).
    /// </summary>
    public double BackoffFactor { get; init; } = 2.0; // exponential

    /// <summary>
    /// Maximum delay allowed between retries. Computed delays are capped at this value.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Random jitter factor applied to each computed delay to reduce lockstep retries.
    /// The actual delay is adjusted within +/- (JitterFactor * computedDelay).
    /// Set to 0 to disable jitter.
    /// </summary>
    public double JitterFactor { get; init; } = 0.1; // +/-10%

    /// <summary>
    /// If true, the first retry after an initial failure is executed without delay.
    /// Defaults to true.
    /// </summary>
    public bool FastFirst { get; init; } = true;

    /// <summary>
    /// Optional predicate to determine if a given exception should be retried.
    /// If null, all exceptions (except cancellations) are considered retriable.
    /// </summary>
    public Func<Exception, bool>? ShouldRetry { get; init; }

    /// <summary>
    /// Optional callback invoked on each retry with the attempt number (starting at 1),
    /// the triggering exception, and the scheduled delay for that attempt.
    /// </summary>
    public Action<int, Exception, TimeSpan>? OnRetry { get; init; }
}
