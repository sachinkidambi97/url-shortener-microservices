using UrlShortener.ShorteningApi.Models;

namespace UrlShortener.ShorteningApi.Repositories;

public interface IUrlRepository
{
    Task<ShortenedUrl?> GetByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<ShortenedUrl> CreateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task<ShortenedUrl> UpdateAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task DeleteAsync(ShortenedUrl shortenedUrl, CancellationToken cancellationToken = default);
    Task<bool> ExistsByShortCodeAsync(string shortCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShortenedUrl>> GetExpiredAsync(CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IReadOnlyList<ShortenedUrl> urls, CancellationToken cancellationToken = default);
}
