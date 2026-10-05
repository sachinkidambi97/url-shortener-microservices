using Microsoft.AspNetCore.Mvc;
using UrlShortener.RedirectService.Services;

namespace UrlShortener.RedirectService.Controllers;

[ApiController]
public sealed class RedirectController(IRedirectService redirectService) : ControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> RedirectAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["X-Correlation-Id"] as string;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var referrer = Request.Headers.Referer.ToString();

        var result = await redirectService.ResolveAsync(
            code, ipAddress, userAgent, referrer, correlationId, cancellationToken);

        return result.Kind switch
        {
            UrlResolution.ResultKind.Found => Redirect(result.OriginalUrl!),
            UrlResolution.ResultKind.Expired => StatusCode(StatusCodes.Status410Gone),
            _ => NotFound()
        };
    }
}
