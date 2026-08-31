using Application.Modules.Projects.CreateProjectInvitation;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.CreateProjectInvitation;

/// <summary>
/// EF Core persistence adapter for the create-project-invitation slice.
/// </summary>
public sealed class EfCreateProjectInvitationStore : ICreateProjectInvitationStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfCreateProjectInvitationStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectInvitationCreationContext?> GetOwnedProjectContextAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var context = await _dbContext.Projects
            .Where(project => project.Id == projectId && project.OwnerId == ownerId)
            .Select(project => new
            {
                project.Name,
                OwnerDisplayName = project.Members
                    .Where(member => member.UserId == ownerId)
                    .Select(member => member.User.DisplayName)
                    .Single()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return context is null
            ? null
            : new ProjectInvitationCreationContext(
                context.Name,
                context.OwnerDisplayName.Value);
    }

    public async Task<User?> GetActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(
            user => user.IsActive && user.Email == normalizedEmail,
            cancellationToken);
    }

    public Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers.AnyAsync(
            member => member.ProjectId == projectId && member.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectInvitation>> GetPendingInvitationsAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _dbContext.ProjectInvitations
            .Where(invitation => invitation.ProjectId == projectId
                && invitation.InvitedUserId == userId
                && invitation.Status == ProjectInvitationStatus.Pending)
            .ToListAsync(cancellationToken);

    public void AddInvitation(ProjectInvitation invitation)
        => _dbContext.ProjectInvitations.Add(invitation);

    public void AddActivity(ProjectActivity activity)
        => _dbContext.ProjectActivities.Add(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
