namespace UrlShortener.Shared.Messaging;

/// <summary>
/// RabbitMQ message published when a URL redirect click is recorded.
/// Exchange: "url-shortener" (topic), routing key: "click.recorded"
/// Queue: "analytics-click-events"
/// </summary>
public sealed record ClickEventMessage
{
    public required string ShortCode { get; init; }
    public required DateTimeOffset ClickedAt { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Referrer { get; init; }
    public string? CorrelationId { get; init; }
}
