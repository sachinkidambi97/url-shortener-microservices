namespace UrlShortener.RedirectService.Services;

public interface IRedirectService
{
    Task<UrlResolution> ResolveAsync(
        string shortCode,
        string? ipAddress,
        string? userAgent,
        string? referrer,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
