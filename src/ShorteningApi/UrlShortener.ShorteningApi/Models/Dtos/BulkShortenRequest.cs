namespace UrlShortener.ShorteningApi.Models.Dtos;

public sealed record BulkShortenRequest
{
    public required List<ShortenRequest> Items { get; init; }
}
