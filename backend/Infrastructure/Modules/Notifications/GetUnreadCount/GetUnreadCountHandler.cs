using Application.Modules.Notifications.GetUnreadCount;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.GetUnreadCount;

public sealed class GetUnreadCountHandler : IGetUnreadCountHandler
{
    private readonly IGetUnreadCountStore _store;

    public GetUnreadCountHandler(IGetUnreadCountStore store) => _store = store;

    public async Task<ApiResponse<int>> HandleAsync(GetUnreadCountQuery query, CancellationToken cancellationToken = default)
        => ApiResponse<int>.Success(await _store.QueryAsync(query.UserId, cancellationToken));
}