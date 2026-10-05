using Microsoft.AspNetCore.Mvc;
using UrlShortener.ShorteningApi.Models.Dtos;
using UrlShortener.ShorteningApi.Repositories;

namespace UrlShortener.ShorteningApi.Controllers;

/// <summary>
/// Internal endpoint — no auth, for service-to-service calls (e.g. Redirect Service).
/// </summary>
[ApiController]
[Route("internal")]
public sealed class InternalController(IUrlRepository urlRepository) : ControllerBase
{
    [HttpGet("urls/{code}")]
    [ProducesResponseType(typeof(InternalUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetByCodeAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var url = await urlRepository.GetByShortCodeAsync(code, cancellationToken);

        if (url is null)
            return NotFound();

        if (url.ExpiresAt.HasValue && url.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return StatusCode(StatusCodes.Status410Gone);

        return Ok(new InternalUrlResponse
        {
            ShortCode = url.ShortCode,
            OriginalUrl = url.OriginalUrl
        });
    }
}
