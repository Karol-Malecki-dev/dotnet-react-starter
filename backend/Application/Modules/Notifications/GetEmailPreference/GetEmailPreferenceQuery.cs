using Application.DTOs.Notification;
using MediatR;
using Shared.Responses;

namespace Application.Modules.Notifications.GetEmailPreference;

public sealed record GetEmailPreferenceQuery(Guid UserId)
    : IRequest<ApiResponse<NotificationEmailPreferenceDto>>;

public interface IGetEmailPreferenceStore
{
    Task<NotificationEmailPreferenceDto> QueryAsync(Guid userId, CancellationToken cancellationToken = default);
}