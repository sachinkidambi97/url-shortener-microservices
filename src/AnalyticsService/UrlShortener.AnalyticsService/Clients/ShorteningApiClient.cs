using System.Text.Json;

namespace UrlShortener.AnalyticsService.Clients;

public sealed record InternalUrlResponse(string ShortCode, string OriginalUrl);

public sealed class ShorteningApiClient(
    HttpClient httpClient,
    ILogger<ShorteningApiClient> logger)
{
    public async Task<InternalUrlResponse?> GetUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"/internal/urls/{shortCode}", cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                response.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<InternalUrlResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to get URL info for short code {ShortCode} from Shortening API", shortCode);
            return null;
        }
    }
}
