using UrlShortener.AnalyticsService.Models;

namespace UrlShortener.AnalyticsService.Repositories;

public interface IClickEventRepository
{
    Task AddAsync(ClickEvent clickEvent, CancellationToken cancellationToken = default);
    Task<long> GetTotalClicksAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> GetLastClickedAtAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClickEvent>> GetRecentClicksAsync(string shortCode, int limit = 20, CancellationToken cancellationToken = default);
}
