using Application.DTOs.Notification;
using Shared.Responses;

namespace Application.Modules.Notifications.GetEmailPreference;

public sealed record GetEmailPreferenceQuery(Guid UserId);

public interface IGetEmailPreferenceHandler
{
    Task<ApiResponse<NotificationEmailPreferenceDto>> HandleAsync(GetEmailPreferenceQuery query, CancellationToken cancellationToken = default);
}

public interface IGetEmailPreferenceStore
{
    Task<NotificationEmailPreferenceDto> QueryAsync(Guid userId, CancellationToken cancellationToken = default);
}