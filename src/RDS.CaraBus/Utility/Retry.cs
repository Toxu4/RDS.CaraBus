using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RDS.CaraBus.Utility
{
    public static class Retry
    {
        public static Task WithRetriesAsync(Func<Task> action, int maxAttempts = 5, TimeSpan? retryInterval = null, CancellationToken cancellationToken = default(CancellationToken), ILogger logger = null)
        {
            return WithRetriesAsync(() => action().ContinueWith(t => Task.CompletedTask, TaskContinuationOptions.OnlyOnRanToCompletion), maxAttempts, retryInterval, cancellationToken, logger);
        }

        public static async Task<T> WithRetriesAsync<T>(Func<Task<T>> action, int maxAttempts = 5, TimeSpan? retryInterval = null, CancellationToken cancellationToken = default(CancellationToken), ILogger logger = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var attempts = 1;
            var startTime = DateTime.UtcNow;
            var currentBackoffTime = DefaultBackoffIntervals[0];
            if (retryInterval != null)
            {
                currentBackoffTime = (int)retryInterval.Value.TotalMilliseconds;
            }

            do
            {
                if (attempts > 1 && logger != null && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Retrying {Attempts} attempt after {Delay:g}...", attempts.ToString(), DateTime.UtcNow.Subtract(startTime));
                }

                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (attempts >= maxAttempts)
                    {
                        throw;
                    }

                    if (logger != null && logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogWarning(ex, "Retry error: {Message}", ex.Message);
                    }

                    await Task.Delay(currentBackoffTime, cancellationToken).ConfigureAwait(false);
                }

                if (retryInterval == null)
                {
                    currentBackoffTime = DefaultBackoffIntervals[Math.Min(attempts, DefaultBackoffIntervals.Length - 1)];
                }

                attempts++;
            } while (attempts <= maxAttempts && !cancellationToken.IsCancellationRequested);

            throw new TaskCanceledException("Should not get here.");
        }

        private static readonly int[] DefaultBackoffIntervals = { 100, 1000, 2000, 2000, 5000, 5000, 10000, 30000, 60000 };
    }
}
