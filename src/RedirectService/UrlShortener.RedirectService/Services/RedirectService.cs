using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrlShortener.RedirectService.Data;
using UrlShortener.RedirectService.Models;
using UrlShortener.Shared.Infrastructure;
using UrlShortener.Shared.Messaging;
using UrlShortener.Shared.Services;

namespace UrlShortener.RedirectService.Services;

public sealed class RedirectService(
    RedirectDbContext dbContext,
    ICacheService cacheService,
    IClickEventPublisher clickEventPublisher,
    IOptions<CacheOptions> cacheOptions,
    ILogger<RedirectService> logger) : IRedirectService
{
    private readonly string _keyPrefix = cacheOptions.Value.KeyPrefix;
    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(cacheOptions.Value.DefaultTtlHours);

    public async Task<UrlResolution> ResolveAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{_keyPrefix}{shortCode}";

        // Cache-aside: check Redis first
        var cachedUrl = await cacheService.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cachedUrl is not null)
        {
            logger.LogDebug("Cache hit for short code {ShortCode}", shortCode);
            await PublishClickAsync(shortCode, ipAddress, userAgent, referrer, correlationId, cancellationToken);
            return UrlResolution.Found(cachedUrl);
        }

        // DB fallback
        var url = await dbContext.ShortenedUrls
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken)
            .ConfigureAwait(false);

        if (url is null)
        {
            logger.LogInformation("Short code {ShortCode} not found", shortCode);
            return UrlResolution.NotFound();
        }

        if (url.ExpiresAt.HasValue && url.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            logger.LogInformation("Short code {ShortCode} has expired", shortCode);
            return UrlResolution.Expired();
        }

        // Populate cache
        await cacheService.SetAsync(cacheKey, url.OriginalUrl, _cacheTtl, cancellationToken).ConfigureAwait(false);

        await PublishClickAsync(shortCode, ipAddress, userAgent, referrer, correlationId, cancellationToken);

        return UrlResolution.Found(url.OriginalUrl);
    }

    private async Task PublishClickAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var message = new ClickEventMessage
        {
            ShortCode = shortCode,
            ClickedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Referrer = referrer,
            CorrelationId = correlationId
        };

        await clickEventPublisher.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }
}

public sealed record UrlResolution
{
    public enum ResultKind { Found, NotFound, Expired }

    public ResultKind Kind { get; private init; }
    public string? OriginalUrl { get; private init; }

    public static UrlResolution Found(string url) => new() { Kind = ResultKind.Found, OriginalUrl = url };
    public static UrlResolution NotFound() => new() { Kind = ResultKind.NotFound };
    public static UrlResolution Expired() => new() { Kind = ResultKind.Expired };
}
