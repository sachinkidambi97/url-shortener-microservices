namespace UrlShortener.AnalyticsService.Models.Dtos;

public sealed record StatsResponse
{
    public required string ShortCode { get; init; }
    public required string OriginalUrl { get; init; }
    public required long TotalClicks { get; init; }
    public required DateTimeOffset? LastClickedAt { get; init; }
    public required IReadOnlyList<ClickDto> RecentClicks { get; init; }
}
