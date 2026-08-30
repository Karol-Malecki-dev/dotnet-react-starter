using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDetails;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.GetProjectDetails;

/// <summary>
/// Reads project details while enforcing project visibility in the database query.
/// </summary>
public sealed class EfGetProjectDetailsStore : IGetProjectDetailsStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfGetProjectDetailsStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectView?> QueryAsync(
        Guid userId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(project => project.Id == projectId
                && (project.OwnerId == userId || project.Members.Any(member => member.UserId == userId && member.User.IsActive))
                && (includeArchived || !project.IsArchived), cancellationToken);

        return project is null
            ? null
            : new ProjectView(
                project.Id,
                project.Name,
                project.Description,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.ConcurrencyStamp,
                project.IsArchived,
                project.OwnerId == userId
                    ? ProjectMemberRole.Owner
                    : project.Members.FirstOrDefault(member => member.UserId == userId)?.Role ?? ProjectMemberRole.Viewer);
    }
}
