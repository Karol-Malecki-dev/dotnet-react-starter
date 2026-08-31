using Application.Features.Projects;
using Application.Modules.Projects.ListProjectInvitations;
using Infrastructure.Data;
using Infrastructure.Modules.Projects.Invitations;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ListProjectInvitations;

/// <summary>
/// EF Core projection for invitations created for one project.
/// </summary>
public sealed class EfListProjectInvitationsStore : IListProjectInvitationsStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListProjectInvitationsStore(ApplicationDbContext dbContext)
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

    public async Task<IReadOnlyList<ProjectInvitationView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _dbContext.ProjectInvitations
            .AsNoTracking()
            .Include(invitation => invitation.Project)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .Where(invitation => invitation.ProjectId == projectId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);

        return invitations.Select(ProjectInvitationViewMapper.Map).ToList();
    }
}
