using UrlShortener.AnalyticsService.Clients;
using UrlShortener.AnalyticsService.Models.Dtos;
using UrlShortener.AnalyticsService.Repositories;

namespace UrlShortener.AnalyticsService.Services;

public sealed class AnalyticsService(
    IClickEventRepository clickEventRepository,
    ShorteningApiClient shorteningApiClient,
    ILogger<AnalyticsService> logger) : IAnalyticsService
{
    public async Task<StatsResponse?> GetStatsAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        // Verify the URL exists in the Shortening API
        var urlInfo = await shorteningApiClient.GetUrlAsync(shortCode, cancellationToken).ConfigureAwait(false);
        if (urlInfo is null)
        {
            logger.LogDebug("Short code {ShortCode} not found in Shortening API", shortCode);
            return null;
        }

        var totalClicks = await clickEventRepository.GetTotalClicksAsync(shortCode, cancellationToken).ConfigureAwait(false);
        var lastClickedAt = await clickEventRepository.GetLastClickedAtAsync(shortCode, cancellationToken).ConfigureAwait(false);
        var recentClicks = await clickEventRepository.GetRecentClicksAsync(shortCode, 20, cancellationToken).ConfigureAwait(false);

        var clickDtos = recentClicks.Select(c => new ClickDto
        {
            ClickedAt = c.ClickedAt,
            IpAddress = c.IpAddress,
            UserAgent = c.UserAgent,
            Referrer = c.Referrer
        }).ToList();

        return new StatsResponse
        {
            ShortCode = shortCode,
            OriginalUrl = urlInfo.OriginalUrl,
            TotalClicks = totalClicks,
            LastClickedAt = lastClickedAt,
            RecentClicks = clickDtos
        };
    }
}
