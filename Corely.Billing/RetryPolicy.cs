namespace Corely.Billing;

internal static class RetryPolicy
{
    private static readonly Random s_random = new();

    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new RetryOptions();
        await ExecuteInternalAsync(
            async ct =>
            {
                await action(ct);
                return 0;
            },
            options,
            cancellationToken
        );
    }

    public static async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        RetryOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new RetryOptions();
        return await ExecuteInternalAsync(action, options, cancellationToken);
    }

    private static async Task<TResult> ExecuteInternalAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        RetryOptions options,
        CancellationToken ct
    )
    {
        if (options.MaxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxAttempts));

        // If the caller already requested cancellation, propagate immediately.
        ct.ThrowIfCancellationRequested();

        var attempt = 0;
        Exception? last = null;

        while (attempt < options.MaxAttempts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ShouldRetry(ex, options))
            {
                last = ex;
                attempt++;
                if (attempt >= options.MaxAttempts)
                    break;

                var delay = NextDelay(attempt, options);
                options.OnRetry?.Invoke(attempt, ex, delay);
                if (!(options.FastFirst && attempt == 1))
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }

        throw last ?? new InvalidOperationException("Retry attempts exhausted.");
    }

    private static bool ShouldRetry(Exception ex, RetryOptions options) =>
        options.ShouldRetry?.Invoke(ex) ?? true;

    private static TimeSpan NextDelay(int attempt, RetryOptions options)
    {
        var exp =
            options.BaseDelay.TotalMilliseconds * Math.Pow(options.BackoffFactor, attempt - 1);
        var ms = Math.Min(exp, options.MaxDelay.TotalMilliseconds);
        if (options.JitterFactor > 0)
        {
            var jitter = 1 + ((s_random.NextDouble() * 2 - 1) * options.JitterFactor);
            ms *= jitter;
        }
        return TimeSpan.FromMilliseconds(ms);
    }
}
