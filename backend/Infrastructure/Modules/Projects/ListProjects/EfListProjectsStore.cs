using Application.Features.Projects;
using Application.Modules.Projects.ListProjects;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ListProjects;

/// <summary>
/// EF Core projection for projects visible to the current user.
/// </summary>
public sealed class EfListProjectsStore : IListProjectsStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListProjectsStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectView>> QueryAsync(
        ListProjectsQuery query,
        CancellationToken cancellationToken = default)
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Where(project => (query.Scope == "owned" ? project.OwnerId == query.UserId
                : query.Scope == "member" ? project.OwnerId != query.UserId
                    && project.Members.Any(member => member.UserId == query.UserId && member.User.IsActive)
                : project.OwnerId == query.UserId
                    || project.Members.Any(member => member.UserId == query.UserId && member.User.IsActive))
                && (query.IncludeArchived || !project.IsArchived))
            .OrderByDescending(project => project.UpdatedAt)
            .Select(project => new ProjectView(
                project.Id,
                project.Name,
                project.Description,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.ConcurrencyStamp,
                project.IsArchived,
                project.OwnerId == query.UserId
                    ? ProjectMemberRole.Owner
                    : project.Members
                        .Where(member => member.UserId == query.UserId)
                        .Select(member => member.Role)
                        .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return projects;
    }
}
