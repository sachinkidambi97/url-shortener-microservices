namespace UrlShortener.ShorteningApi.Models.Dtos;

public sealed record InternalUrlResponse
{
    public required string ShortCode { get; init; }
    public required string OriginalUrl { get; init; }
}
