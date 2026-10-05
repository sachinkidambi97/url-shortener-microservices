using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.AnalyticsService.Models.Dtos;
using UrlShortener.AnalyticsService.Services;

namespace UrlShortener.AnalyticsService.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public sealed class StatsController(IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(StatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatsAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var stats = await analyticsService.GetStatsAsync(code, cancellationToken);
        if (stats is null)
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = $"Short code '{code}' not found.",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(stats);
    }
}
