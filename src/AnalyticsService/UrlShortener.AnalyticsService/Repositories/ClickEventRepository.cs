using Microsoft.EntityFrameworkCore;
using Polly;
using UrlShortener.AnalyticsService.Data;
using UrlShortener.AnalyticsService.Models;
using UrlShortener.Shared.Infrastructure;

namespace UrlShortener.AnalyticsService.Repositories;

public sealed class ClickEventRepository(
    AnalyticsDbContext dbContext,
    ILogger<ClickEventRepository> logger) : IClickEventRepository
{
    private readonly IAsyncPolicy _retryPolicy = ResiliencePolicies.CreateDatabaseRetryPolicy(logger);

    public async Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken = default)
    {
        dbContext.ClickEvents.Add(clickEvent);
        await _retryPolicy.ExecuteAsync(() =>
            dbContext.SaveChangesAsync(cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<long> GetTotalClicksAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ClickEvents
                .AsNoTracking()
                .LongCountAsync(e => e.ShortCode == shortCode, cancellationToken))
            .ConfigureAwait(false);

    public async Task<DateTimeOffset?> GetLastClickedAtAsync(string shortCode, CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ClickEvents
                .AsNoTracking()
                .Where(e => e.ShortCode == shortCode)
                .OrderByDescending(e => e.ClickedAt)
                .Select(e => (DateTimeOffset?)e.ClickedAt)
                .FirstOrDefaultAsync(cancellationToken))
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(
        string shortCode,
        int limit = 20,
        CancellationToken cancellationToken = default)
        => await _retryPolicy.ExecuteAsync(() =>
            dbContext.ClickEvents
                .AsNoTracking()
                .Where(e => e.ShortCode == shortCode)
                .OrderByDescending(e => e.ClickedAt)
                .Take(limit)
                .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
}
