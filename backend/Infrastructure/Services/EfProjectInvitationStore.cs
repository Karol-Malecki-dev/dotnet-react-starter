using Application.Features.Projects;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Services;

/// <summary>
/// EF Core implementation of project invitation persistence operations.
/// </summary>
public sealed class EfProjectInvitationStore : IProjectInvitationStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectInvitationStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.IsActive && user.Email == email, cancellationToken);

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public Task<bool> HasPendingInvitationAsync(
        Guid projectId,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectInvitations.AnyAsync(invitation => invitation.ProjectId == projectId
            && invitation.InvitedUserId == userId
            && invitation.Status == ProjectInvitationStatus.Pending
            && invitation.ExpiresAt > now, cancellationToken);

    public Task<string> GetProjectNameAsync(Guid projectId, CancellationToken cancellationToken = default)
        => _dbContext.Projects.Where(project => project.Id == projectId).Select(project => project.Name).SingleAsync(cancellationToken);

    public Task<string> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.Where(user => user.Id == userId).Select(user => user.DisplayName).SingleAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectInvitation>> GetProjectInvitationsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _dbContext.ProjectInvitations.AsNoTracking()
            .Include(invitation => invitation.Project)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .Where(invitation => invitation.ProjectId == projectId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectInvitation>> GetUserPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.ProjectInvitations.AsNoTracking()
            .Include(invitation => invitation.Project)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .Where(invitation => invitation.InvitedUserId == userId && invitation.Status == ProjectInvitationStatus.Pending)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ProjectInvitation?> GetInvitationWithDetailsAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.ProjectInvitations
            .Include(invitation => invitation.Project)
            .ThenInclude(project => project.Members)
            .ThenInclude(member => member.User)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser)
            .FirstOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);

    public async Task<IProjectInvitationTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return null;
        }

        return new EfProjectInvitationTransaction(await _dbContext.Database.BeginTransactionAsync(cancellationToken));
    }

    public void AddInvitation(ProjectInvitation invitation) => _dbContext.ProjectInvitations.Add(invitation);

    public void AddMember(ProjectMember member) => _dbContext.ProjectMembers.Add(member);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    private sealed class EfProjectInvitationTransaction : IProjectInvitationTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private bool _committed;

        public EfProjectInvitationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                await _transaction.RollbackAsync();
            }

            await _transaction.DisposeAsync();
        }
    }
}