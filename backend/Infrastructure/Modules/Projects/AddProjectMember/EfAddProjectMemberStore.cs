using Application.Modules.Projects.AddProjectMember;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.AddProjectMember;

/// <summary>
/// EF Core persistence adapter for the add-project-member slice.
/// </summary>
public sealed class EfAddProjectMemberStore : IAddProjectMemberStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfAddProjectMemberStore(ApplicationDbContext dbContext)
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

    public Task<User?> GetActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == userId && user.IsActive,
            cancellationToken);

    public Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers.AnyAsync(
            member => member.ProjectId == projectId && member.UserId == userId,
            cancellationToken);

    public void AddMember(ProjectMember member)
        => _dbContext.ProjectMembers.Add(member);

    public void AddActivity(ProjectActivity activity)
        => _dbContext.ProjectActivities.Add(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
