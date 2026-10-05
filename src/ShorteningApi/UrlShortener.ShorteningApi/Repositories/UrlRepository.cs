using Microsoft.EntityFrameworkCore;
using Polly;
using UrlShortener.ShorteningApi.Data;
using UrlShortener.ShorteningApi.Models;
using UrlShortener.Shared.Infrastructure;

namespace UrlShortener.ShorteningApi.Repositories;

public sealed class UrlRepository(ShorteningDbContext dbContext, ILogger<UrlRepository> logger) : IUrlRepository
{
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateDatabaseRetryPolicy(logger);

    public async Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);

    public async Task<ShortenedUrl> CreateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        shortenedUrl.CreatedAt = DateTimeOffset.UtcNow;
        dbContext.ShortenedUrls.Add(shortenedUrl);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
        return shortenedUrl;
    }

    public async Task<ShortenedUrl> UpdateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        dbContext.ShortenedUrls.Update(shortenedUrl);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
        return shortenedUrl;
    }

    public async Task DeleteAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default)
    {
        dbContext.ShortenedUrls.Remove(shortenedUrl);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AnyAsync(u => u.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ShortenedUrl>> GetExpiredAsync(CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ShortenedUrls
                .AsNoTracking()
                .Where(u => u.ExpiresAt.HasValue && u.ExpiresAt.Value <= DateTimeOffset.UtcNow)
                .ToListAsync(cancellationToken))
            .ConfigureAwait(false);

    public async Task DeleteRangeAsync(IReadOnlyList<ShortenedUrl> urls, CancellationToken cancellationToken = default)
    {
        dbContext.ShortenedUrls.RemoveRange(urls);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
    }
}
