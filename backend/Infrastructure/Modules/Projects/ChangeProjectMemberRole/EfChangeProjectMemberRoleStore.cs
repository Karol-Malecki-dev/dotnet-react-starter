using Application.Modules.Projects.ChangeProjectMemberRole;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// EF Core persistence adapter for the change-project-member-role slice.
/// </summary>
public sealed class EfChangeProjectMemberRoleStore : IChangeProjectMemberRoleStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfChangeProjectMemberRoleStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetOwnedProjectWithMembersAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _dbContext.Projects
            .Include(project => project.Members)
            .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(
                project => project.Id == projectId && project.OwnerId == ownerId,
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
