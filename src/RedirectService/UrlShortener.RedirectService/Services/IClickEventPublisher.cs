using UrlShortener.Shared.Messaging;

namespace UrlShortener.RedirectService.Services;

public interface IClickEventPublisher
{
    Task PublishAsync(ClickEventMessage message, CancellationToken cancellationToken = default);
}
