namespace UrlShortener.RedirectService.Models;

/// <summary>
/// Read-only projection of the shortened_urls table.
/// Only contains fields needed for redirect resolution.
/// </summary>
public sealed class ShortenedUrl
{
    public int Id { get; set; }
    public required string ShortCode { get; set; }
    public required string OriginalUrl { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
