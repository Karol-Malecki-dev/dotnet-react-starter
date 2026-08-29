using Application.Features.ProjectManagement.Tasks;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ProjectManagement.Tasks;

/// <summary>
/// EF Core implementation of the persistence operations required by ProjectTask commands.
/// </summary>
public sealed class EfProjectTaskCommandStore : IProjectTaskCommandStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskCommandStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsActiveProjectMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId
            && member.UserId == userId
            && member.User.IsActive, cancellationToken);

    public void AddTask(ProjectTask task) => _dbContext.ProjectTasks.Add(task);

    public void RemoveTask(ProjectTask task) => _dbContext.ProjectTasks.Remove(task);

    public void ReplaceTaskLabels(ProjectTask task, IReadOnlyCollection<ProjectTaskLabel> previousLabels)
    {
        _dbContext.ProjectTaskLabels.RemoveRange(previousLabels);
        _dbContext.ProjectTaskLabels.AddRange(task.Labels);
    }

    public void AddActivity(ProjectActivity activity) => _dbContext.ProjectActivities.Add(activity);

    public void ClearChangeTracker() => _dbContext.ChangeTracker.Clear();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}