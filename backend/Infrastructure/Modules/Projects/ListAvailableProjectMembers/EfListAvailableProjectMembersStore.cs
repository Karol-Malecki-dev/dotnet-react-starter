using Application.Features.Projects;
using Application.Modules.Projects.ListAvailableProjectMembers;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// EF Core projection for active users who are not already members of a project.
/// </summary>
public sealed class EfListAvailableProjectMembersStore : IListAvailableProjectMembersStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListAvailableProjectMembersStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> OwnedProjectExistsAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _dbContext.Projects.AnyAsync(
            project => project.Id == projectId && project.OwnerId == ownerId,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectMemberUserView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var memberIds = _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId)
            .Select(member => member.UserId);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && !memberIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName)
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email
            })
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new ProjectMemberUserView(
                user.Id,
                user.DisplayName.Value,
                user.Email.Value))
            .ToList();
    }
}
