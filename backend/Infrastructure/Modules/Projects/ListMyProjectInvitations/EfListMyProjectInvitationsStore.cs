using Application.Features.Projects;
using Application.Modules.Projects.ListMyProjectInvitations;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Modules.Projects.Invitations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// EF Core projection for pending invitations addressed to one user.
/// </summary>
public sealed class EfListMyProjectInvitationsStore : IListMyProjectInvitationsStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListMyProjectInvitationsStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectInvitationView>> QueryAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _dbContext.ProjectInvitations
            .AsNoTracking()
            .Include(invitation => invitation.Project)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .Where(invitation => invitation.InvitedUserId == userId
                && invitation.Status == ProjectInvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);

        return invitations.Select(ProjectInvitationViewMapper.Map).ToList();
    }
}
