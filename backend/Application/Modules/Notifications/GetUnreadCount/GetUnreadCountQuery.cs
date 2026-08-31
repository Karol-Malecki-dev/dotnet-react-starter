using Shared.Responses;

namespace Application.Modules.Notifications.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId);

public interface IGetUnreadCountHandler
{
    Task<ApiResponse<int>> HandleAsync(GetUnreadCountQuery query, CancellationToken cancellationToken = default);
}

public interface IGetUnreadCountStore
{
    Task<int> QueryAsync(Guid userId, CancellationToken cancellationToken = default);
}