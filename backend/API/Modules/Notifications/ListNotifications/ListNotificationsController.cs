using Application.DTOs.Notification;
using Application.Modules.Notifications.ListNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Modules.Notifications.ListNotifications;

/// <summary>
/// HTTP adapter for the list-notifications vertical slice.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class ListNotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public ListNotificationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Lists notifications belonging to the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<NotificationPageDto>.Error(401, "User not authenticated"));
        }

        var result = await _sender.Send(
            new ListNotificationsQuery(userId, pageNumber, pageSize, unreadOnly),
            cancellationToken);

        return StatusCode(result.StatusCode, result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }
}