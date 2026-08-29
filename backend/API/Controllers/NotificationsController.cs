using Application.DTOs.Notification;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationPageDto>>> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<NotificationPageDto>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.GetUserNotificationsAsync(userId, pageNumber, pageSize, unreadOnly, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<int>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<NotificationDto>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<int>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("email-preference")]
    public async Task<ActionResult<ApiResponse<NotificationEmailPreferenceDto>>> GetEmailPreference(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<NotificationEmailPreferenceDto>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.GetEmailPreferenceAsync(userId, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("email-preference")]
    public async Task<ActionResult<ApiResponse<NotificationEmailPreferenceDto>>> UpdateEmailPreference(
        [FromBody] UpdateNotificationEmailPreferenceDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<NotificationEmailPreferenceDto>.Error(401, "User not authenticated"));
        }

        var result = await _notificationService.UpdateEmailPreferenceAsync(
            userId,
            request.IsEmailEnabled,
            request.IsTaskDeadlineReminderEmailEnabled,
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
