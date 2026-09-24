namespace Corely.Billing.Resilience;

internal static class RetryOptionsExtensions
{
    extension(RetryOptions options)
    {
        public bool IsRetryable(Exception ex) => options.ShouldRetry?.Invoke(ex) ?? true;

        public TimeSpan RetryDelay(int attempt, Random random)
        {
            var exponential =
                options.BaseDelay.TotalMilliseconds * Math.Pow(options.BackoffFactor, attempt - 1);
            var ms = Math.Min(exponential, options.MaxDelay.TotalMilliseconds);
            if (options.JitterFactor > 0)
                ms *= 1 + ((random.NextDouble() * 2 - 1) * options.JitterFactor);
            return TimeSpan.FromMilliseconds(ms);
        }
    }
}
