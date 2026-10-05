using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UrlShortener.AnalyticsService.Models;
using UrlShortener.AnalyticsService.Repositories;
using UrlShortener.Shared.Messaging;

namespace UrlShortener.AnalyticsService.Services;

public sealed class ClickEventConsumer(
    IServiceProvider serviceProvider,
    IConnection rabbitConnection,
    ILogger<ClickEventConsumer> logger) : BackgroundService
{
    private const string ExchangeName = "url-shortener";
    private const string QueueName = "analytics-click-events";
    private const string RoutingKey = "click.recorded";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() =>
            logger.LogInformation("ClickEventConsumer stopping."));

        var channel = rabbitConnection.CreateModel();

        channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueBind(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: RoutingKey);

        // Process one message at a time for guaranteed ordering
        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                var message = JsonSerializer.Deserialize<ClickEventMessage>(body);

                if (message is not null)
                {
                    await ProcessClickAsync(message, stoppingToken).ConfigureAwait(false);
                }

                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process click event message.");
                // Reject and don't requeue (dead-letter if configured)
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(
            queue: QueueName,
            autoAck: false,
            consumer: consumer);

        logger.LogInformation("ClickEventConsumer started. Listening on queue {Queue}", QueueName);

        // Keep running until cancellation
        return Task.Delay(Timeout.Infinite, stoppingToken)
            .ContinueWith(_ =>
            {
                channel.Close();
                channel.Dispose();
            }, TaskScheduler.Default);
    }

    private async Task ProcessClickAsync(ClickEventMessage message, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IClickEventRepository>();

        var clickEvent = new ClickEvent
        {
            ShortCode = message.ShortCode,
            ClickedAt = message.ClickedAt,
            IpAddress = message.IpAddress,
            UserAgent = message.UserAgent,
            Referrer = message.Referrer,
            CorrelationId = message.CorrelationId
        };

        await repository.AddAsync(clickEvent, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Persisted click event for short code {ShortCode}", message.ShortCode);
    }
}
