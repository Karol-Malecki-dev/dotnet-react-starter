using Application.Modules.Projects.RemoveProjectMember;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.RemoveProjectMember;

/// <summary>
/// EF Core persistence adapter for the remove-project-member slice.
/// </summary>
public sealed class EfRemoveProjectMemberStore : IRemoveProjectMemberStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfRemoveProjectMemberStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetOwnedProjectWithMembersAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _dbContext.Projects
            .Include(project => project.Members)
            .FirstOrDefaultAsync(
                project => project.Id == projectId && project.OwnerId == ownerId,
                cancellationToken);

    public void RemoveMember(ProjectMember member)
        => _dbContext.ProjectMembers.Remove(member);

    public void AddActivity(ProjectActivity activity)
        => _dbContext.ProjectActivities.Add(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
