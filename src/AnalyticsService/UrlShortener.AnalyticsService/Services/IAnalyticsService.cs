using UrlShortener.AnalyticsService.Models.Dtos;

namespace UrlShortener.AnalyticsService.Services;

public interface IAnalyticsService
{
    Task<StatsResponse?> GetStatsAsync(string shortCode, CancellationToken cancellationToken = default);
}
