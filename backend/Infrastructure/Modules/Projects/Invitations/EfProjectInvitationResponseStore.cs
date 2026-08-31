using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.Invitations;

/// <summary>
/// EF Core persistence adapter shared by invitation accept and decline slices.
/// </summary>
public sealed class EfProjectInvitationResponseStore : IProjectInvitationResponseStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectInvitationResponseStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectInvitations
            .Include(invitation => invitation.Project)
            .ThenInclude(project => project.Members)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .FirstOrDefaultAsync(
                invitation => invitation.TokenHash == tokenHash,
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
