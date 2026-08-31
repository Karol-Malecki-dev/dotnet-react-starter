using Application.Modules.Notifications.GetUnreadCount;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications.GetUnreadCount;

public sealed class EfGetUnreadCountStore : IGetUnreadCountStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfGetUnreadCountStore(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public Task<int> QueryAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Set<Notification>().CountAsync(
            notification => notification.UserId == userId && notification.ReadAt == null,
            cancellationToken);
}