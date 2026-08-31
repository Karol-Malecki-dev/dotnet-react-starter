using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDashboard;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.GetProjectDashboard;

/// <summary>
/// EF Core adapter for project-owned dashboard data.
/// </summary>
public sealed class EfGetProjectDashboardStore : IGetProjectDashboardStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfGetProjectDashboardStore(ApplicationDbContext dbContext)
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

    public async Task<IReadOnlyList<ProjectActivityView>> GetRecentActivityAsync(
        Guid projectId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var projected = await _dbContext.ProjectActivities
            .AsNoTracking()
            .Where(activity => activity.ProjectId == projectId)
            .OrderByDescending(activity => activity.CreatedAt)
            .Take(count)
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

        return projected
            .Select(activity => new ProjectActivityView(
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                activity.ActorDisplayName.Value,
                activity.ProjectTaskId,
                activity.CreatedAt))
            .ToList();
    }
}
