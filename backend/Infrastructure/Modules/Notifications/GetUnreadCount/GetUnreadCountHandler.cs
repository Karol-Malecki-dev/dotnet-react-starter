using Application.Modules.Notifications.GetUnreadCount;
using MediatR;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.GetUnreadCount;

public sealed class GetUnreadCountHandler
    : IRequestHandler<GetUnreadCountQuery, ApiResponse<int>>
{
    private readonly IGetUnreadCountStore _store;

    public GetUnreadCountHandler(IGetUnreadCountStore store) => _store = store;

    public async Task<ApiResponse<int>> Handle(GetUnreadCountQuery query, CancellationToken cancellationToken)
        => ApiResponse<int>.Success(await _store.QueryAsync(query.UserId, cancellationToken));
}