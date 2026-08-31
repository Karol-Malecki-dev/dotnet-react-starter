using Application.Features.Projects;
using Application.Modules.Projects.GetProjectActivity;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.GetProjectActivity;

/// <summary>
/// EF Core projection for paged project activity.
/// </summary>
public sealed class EfGetProjectActivityStore : IGetProjectActivityStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfGetProjectActivityStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasProjectAccessAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _dbContext.Projects.AnyAsync(
            project => project.Id == projectId
                && (project.OwnerId == userId
                    || project.Members.Any(member =>
                        member.UserId == userId && member.User.IsActive)),
            cancellationToken);

    public async Task<PagedProjectActivityView> QueryAsync(
        Guid projectId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ProjectActivities
            .AsNoTracking()
            .Where(activity => activity.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var projected = await query
            .OrderByDescending(activity => activity.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(activity => new
            {
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                ActorDisplayName = activity.ActorUser.DisplayName,
                activity.ProjectTaskId,
                activity.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var items = projected
            .Select(activity => new ProjectActivityView(
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                activity.ActorDisplayName.Value,
                activity.ProjectTaskId,
                activity.CreatedAt))
            .ToList();
        return new PagedProjectActivityView(items, pageNumber, pageSize, totalCount);
    }
}
