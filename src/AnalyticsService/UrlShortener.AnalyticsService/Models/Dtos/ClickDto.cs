namespace UrlShortener.AnalyticsService.Models.Dtos;

public sealed record ClickDto
{
    public required DateTimeOffset ClickedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Referrer { get; init; }
}
