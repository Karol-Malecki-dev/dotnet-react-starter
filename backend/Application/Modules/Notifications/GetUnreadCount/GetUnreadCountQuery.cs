using MediatR;
using Shared.Responses;

namespace Application.Modules.Notifications.GetUnreadCount;

public sealed record GetUnreadCountQuery(Guid UserId)
    : IRequest<ApiResponse<int>>;

public interface IGetUnreadCountStore
{
    Task<int> QueryAsync(Guid userId, CancellationToken cancellationToken = default);
}