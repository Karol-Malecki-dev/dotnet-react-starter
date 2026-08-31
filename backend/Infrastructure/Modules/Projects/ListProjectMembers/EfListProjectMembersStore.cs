using Application.Features.Projects;
using Application.Modules.Projects.ListProjectMembers;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ListProjectMembers;

/// <summary>
/// EF Core projection for active members of a visible project.
/// </summary>
public sealed class EfListProjectMembersStore : IListProjectMembersStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListProjectMembersStore(ApplicationDbContext dbContext)
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
                    || project.Members.Any(member => member.UserId == userId && member.User.IsActive)),
            cancellationToken);

    public async Task<IReadOnlyList<ProjectMemberView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.User.IsActive)
            .OrderBy(member => member.User.DisplayName)
            .Select(member => new
            {
                member.UserId,
                member.User.DisplayName,
                Email = member.User.Email,
                member.Role,
                member.AddedAt
            })
            .ToListAsync(cancellationToken);

        return members
            .Select(member => new ProjectMemberView(
                member.UserId,
                member.DisplayName.Value,
                member.Email.Value,
                member.Role,
                member.AddedAt))
            .ToList();
    }
}
