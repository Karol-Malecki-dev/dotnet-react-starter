using Application.Modules.ProjectTasks.Assignments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.Assignments;

/// <summary>
/// Stages task unassignments in the shared EF Core unit of work.
/// </summary>
public sealed class EfProjectTaskMemberAssignmentWriter : IProjectTaskMemberAssignmentWriter
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskMemberAssignmentWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UnassignAllAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await _dbContext.ProjectTasks
            .Where(task => task.ProjectId == projectId && task.AssignedUserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var task in tasks)
        {
            task.Unassign();
        }
    }
}
