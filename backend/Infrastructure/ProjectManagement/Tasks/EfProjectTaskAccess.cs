using Application.Features.ProjectManagement.Tasks;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ProjectManagement.Tasks;

/// <summary>
/// EF Core implementation of the persistence operations needed by ProjectTask use cases.
/// </summary>
public sealed class EfProjectTaskAccess : IProjectTaskAccess
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskAccess(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectMemberRole?> GetActiveProjectRoleAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.Id == projectId && !project.IsArchived, cancellationToken);
        if (project is null)
        {
            return null;
        }

        if (project.OwnerId == userId)
        {
            return ProjectMemberRole.Owner;
        }

        return await _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == userId && member.User.IsActive)
            .Select(member => (ProjectMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProjectTask?> GetTaskWithLabelsAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectTasks
            .Where(task => task.Id == taskId && task.ProjectId == projectId)
            .Include(task => task.Labels)
            .FirstOrDefaultAsync(cancellationToken);
}