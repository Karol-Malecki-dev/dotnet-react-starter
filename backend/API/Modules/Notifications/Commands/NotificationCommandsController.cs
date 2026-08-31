using Application.DTOs.Notification;
using Application.Modules.Notifications.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Modules.Notifications.Commands;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationCommandsController : ControllerBase
{
    private readonly IMarkNotificationAsReadHandler _markAsRead;
    private readonly IMarkAllNotificationsAsReadHandler _markAllAsRead;
    private readonly IUpdateNotificationEmailPreferenceHandler _updatePreference;

    public NotificationCommandsController(
        IMarkNotificationAsReadHandler markAsRead,
        IMarkAllNotificationsAsReadHandler markAllAsRead,
        IUpdateNotificationEmailPreferenceHandler updatePreference)
    {
        _markAsRead = markAsRead;
        _markAllAsRead = markAllAsRead;
        _updatePreference = updatePreference;
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
        => await ExecuteAsync(userId => _markAsRead.HandleAsync(new MarkNotificationAsReadCommand(userId, id), cancellationToken), ApiResponse<NotificationDto>.Error(401, "User not authenticated"));

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
        => await ExecuteAsync(userId => _markAllAsRead.HandleAsync(new MarkAllNotificationsAsReadCommand(userId), cancellationToken), ApiResponse<int>.Error(401, "User not authenticated"));

    [HttpPatch("email-preference")]
    public async Task<IActionResult> UpdateEmailPreference([FromBody] UpdateNotificationEmailPreferenceDto request, CancellationToken cancellationToken = default)
        => await ExecuteAsync(userId => _updatePreference.HandleAsync(new UpdateNotificationEmailPreferenceCommand(userId, request.IsEmailEnabled, request.IsTaskDeadlineReminderEmailEnabled), cancellationToken), ApiResponse<NotificationEmailPreferenceDto>.Error(401, "User not authenticated"));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Guid, Task<ApiResponse<T>>> action, ApiResponse<T> unauthorized)
    {
        var value = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !Guid.TryParse(value, out var userId)
            ? Unauthorized(unauthorized)
            : ToActionResult(await action(userId));
    }

    private IActionResult ToActionResult<T>(ApiResponse<T> response) => StatusCode(response.StatusCode, response);
}