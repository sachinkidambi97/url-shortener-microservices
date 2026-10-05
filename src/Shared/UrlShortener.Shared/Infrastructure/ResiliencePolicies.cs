using Npgsql;
using Polly;
using StackExchange.Redis;

namespace UrlShortener.Shared.Infrastructure;

/// <summary>
/// Defines Polly retry policies for transient failures in PostgreSQL and Redis.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry policy for transient PostgreSQL exceptions: 3 retries with exponential backoff.
    /// </summary>
    public static IAsyncPolicy CreateDatabaseRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<NpgsqlException>(ex => ex.IsTransient)
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, attempt, _) =>
                {
                    logger.LogWarning(exception,
                        "Database transient failure. Retrying attempt {Attempt} after {Delay}s.",
                        attempt, timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// Retry policy for transient Redis exceptions: 3 retries with exponential backoff.
    /// </summary>
    public static IAsyncPolicy CreateRedisRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, attempt, _) =>
                {
                    logger.LogWarning(exception,
                        "Redis transient failure. Retrying attempt {Attempt} after {Delay}s.",
                        attempt, timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// Retry policy for HTTP clients: 3 retries with exponential backoff on transient failures.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateHttpRetryPolicy(ILogger logger)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timeSpan, attempt, _) =>
                {
                    logger.LogWarning(outcome.Exception,
                        "HTTP request transient failure. Retrying attempt {Attempt} after {Delay}s.",
                        attempt, timeSpan.TotalSeconds);
                });
    }
}
