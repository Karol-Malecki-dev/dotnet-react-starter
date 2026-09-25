using Application.DTOs.Notification;
using Application.Modules.Notifications.GetEmailPreference;
using MediatR;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.GetEmailPreference;

public sealed class GetEmailPreferenceHandler
    : IRequestHandler<GetEmailPreferenceQuery, ApiResponse<NotificationEmailPreferenceDto>>
{
    private readonly IGetEmailPreferenceStore _store;

    public GetEmailPreferenceHandler(IGetEmailPreferenceStore store) => _store = store;

    public async Task<ApiResponse<NotificationEmailPreferenceDto>> Handle(
        GetEmailPreferenceQuery query,
        CancellationToken cancellationToken)
        => ApiResponse<NotificationEmailPreferenceDto>.Success(await _store.QueryAsync(query.UserId, cancellationToken));
}