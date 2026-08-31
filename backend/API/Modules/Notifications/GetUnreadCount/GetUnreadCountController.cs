using Application.Modules.Notifications.GetUnreadCount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Modules.Notifications.GetUnreadCount;

[ApiController]
[Route("api/notifications/unread-count")]
[Authorize]
public sealed class GetUnreadCountController : ControllerBase
{
    private readonly IGetUnreadCountHandler _handler;

    public GetUnreadCountController(IGetUnreadCountHandler handler) => _handler = handler;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            return Unauthorized(ApiResponse<int>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(new GetUnreadCountQuery(userId), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}