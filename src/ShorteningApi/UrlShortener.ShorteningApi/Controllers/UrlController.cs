using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.ShorteningApi.Models.Dtos;
using UrlShortener.ShorteningApi.Repositories;
using UrlShortener.ShorteningApi.Services;

namespace UrlShortener.ShorteningApi.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[EnableRateLimiting("fixed")]
public sealed class UrlController(
    IUrlShorteningService urlShorteningService,
    IUserRepository userRepository) : ControllerBase
{
    [HttpPost("shorten")]
    [ProducesResponseType(typeof(ShortenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ShortenAsync(
        [FromBody] ShortenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ShortenResponse response = await urlShorteningService.ShortenAsync(request, baseUrl, userId, cancellationToken);
        return Created(response.ShortUrl, response);
    }

    [HttpPost("shorten/bulk")]
    [ProducesResponseType(typeof(BulkShortenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkShortenAsync(
        [FromBody] BulkShortenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        BulkShortenResponse response = await urlShorteningService.BulkShortenAsync(request, baseUrl, userId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("urls")]
    [ProducesResponseType(typeof(IReadOnlyList<UrlListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyUrlsAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var urls = await userRepository.GetUrlsByUserIdAsync(userId.Value, cancellationToken);

        var items = urls.Select(u => new UrlListItem
        {
            ShortCode = u.ShortCode,
            ShortUrl = $"{baseUrl.TrimEnd('/')}/{u.ShortCode}",
            OriginalUrl = u.OriginalUrl,
            CreatedAt = u.CreatedAt,
            ExpiresAt = u.ExpiresAt
        }).ToList();

        return Ok(items);
    }

    [HttpPut("urls/{code}")]
    [ProducesResponseType(typeof(ShortenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUrlAsync(
        [FromRoute] string code,
        [FromBody] UpdateUrlRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await urlShorteningService.UpdateUrlAsync(code, request.Url, userId.Value, baseUrl, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("urls/{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUrlAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        await urlShorteningService.DeleteUrlAsync(code, userId.Value, cancellationToken);
        return NoContent();
    }

    private int? GetUserIdFromClaims()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (sub is not null && int.TryParse(sub, out var id))
            return id;

        return null;
    }
}
