using Application.DTOs.Notification;
using Application.Modules.Notifications.GetEmailPreference;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Modules.Notifications.GetEmailPreference;

[ApiController]
[Route("api/notifications/email-preference")]
[Authorize]
public sealed class GetEmailPreferenceController : ControllerBase
{
    private readonly IGetEmailPreferenceHandler _handler;

    public GetEmailPreferenceController(IGetEmailPreferenceHandler handler) => _handler = handler;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            return Unauthorized(ApiResponse<NotificationEmailPreferenceDto>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(new GetEmailPreferenceQuery(userId), cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}