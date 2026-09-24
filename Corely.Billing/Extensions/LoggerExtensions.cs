using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Extensions;

internal static class LoggerExtensions
{
    extension(ILogger logger)
    {
        public async Task<TResult> ExecuteWithLoggingAsync<TRequest, TResult>(
            string className,
            TRequest request,
            Func<Task<TResult>> operation,
            bool logResult = false,
            [CallerMemberName] string methodName = ""
        )
        {
            try
            {
                ArgumentNullException.ThrowIfNull(request, nameof(request));

                logger.LogTrace(
                    "[{Class}] {Method} starting with request {@Request}",
                    className,
                    methodName,
                    request
                );

                var stopwatch = Stopwatch.StartNew();
                var result = await operation();
                stopwatch.Stop();

                if (logResult)
                {
                    logger.LogTrace(
                        "[{Class}] {Method} completed in {ElapsedMs} ms with result {@Result}",
                        className,
                        methodName,
                        stopwatch.ElapsedMilliseconds,
                        result
                    );
                }
                else
                {
                    logger.LogTrace(
                        "[{Class}] {Method} completed in {ElapsedMs} ms",
                        className,
                        methodName,
                        stopwatch.ElapsedMilliseconds
                    );
                }

                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{Class}] {Method} failed", className, methodName);
                throw;
            }
        }
    }
}
