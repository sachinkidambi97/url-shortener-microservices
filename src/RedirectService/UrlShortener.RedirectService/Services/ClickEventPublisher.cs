using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using UrlShortener.Shared.Messaging;

namespace UrlShortener.RedirectService.Services;

public sealed class ClickEventPublisher(
    IConnection rabbitConnection,
    ILogger<ClickEventPublisher> logger) : IClickEventPublisher
{
    private const string ExchangeName = "url-shortener";
    private const string RoutingKey = "click.recorded";

    public Task PublishAsync(ClickEventMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var channel = rabbitConnection.CreateModel();

            channel.ExchangeDeclare(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var props = channel.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent

            channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: RoutingKey,
                basicProperties: props,
                body: body);

            logger.LogDebug("Published click event for short code {ShortCode}", message.ShortCode);
        }
        catch (Exception ex)
        {
            // Non-fatal: redirect still works even if analytics fails
            logger.LogWarning(ex, "Failed to publish click event for short code {ShortCode}", message.ShortCode);
        }

        return Task.CompletedTask;
    }
}
