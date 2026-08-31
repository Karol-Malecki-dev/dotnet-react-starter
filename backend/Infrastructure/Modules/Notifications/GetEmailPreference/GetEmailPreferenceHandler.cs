using Application.DTOs.Notification;
using Application.Modules.Notifications.GetEmailPreference;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.GetEmailPreference;

public sealed class GetEmailPreferenceHandler : IGetEmailPreferenceHandler
{
    private readonly IGetEmailPreferenceStore _store;

    public GetEmailPreferenceHandler(IGetEmailPreferenceStore store) => _store = store;

    public async Task<ApiResponse<NotificationEmailPreferenceDto>> HandleAsync(GetEmailPreferenceQuery query, CancellationToken cancellationToken = default)
        => ApiResponse<NotificationEmailPreferenceDto>.Success(await _store.QueryAsync(query.UserId, cancellationToken));
}