using Application.Features.Projects;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// EF Core implementation of project membership persistence operations.
/// </summary>
public sealed class EfProjectMembershipStore : IProjectMembershipStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectMembershipStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> OwnedProjectExistsAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId && project.OwnerId == ownerId, cancellationToken);

    public Task<Project?> GetOwnedProjectWithMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _dbContext.Projects
            .Include(project => project.Members)
            .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(project => project.Id == projectId && project.OwnerId == ownerId, cancellationToken);

    public Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId
            && (project.OwnerId == userId || project.Members.Any(member => member.UserId == userId && member.User.IsActive)), cancellationToken);

    public async Task<IReadOnlyList<ProjectMemberView>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default)
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
                member.AddedAt })
            .ToListAsync(cancellationToken);

        return members
            .Select(member => new ProjectMemberView(
                member.UserId,
                member.DisplayName,
                member.Email.Value,
                member.Role,
                member.AddedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectMemberUserView>> GetAvailableUsersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var memberIds = _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId)
            .Select(member => member.UserId);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && !memberIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);

        return users
            .Select(user => new ProjectMemberUserView(user.Id, user.DisplayName, user.Email.Value))
            .ToList();
    }

    public Task<User?> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken);

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public async Task<IProjectTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return null;
        }

        return new EfProjectTransaction(await _dbContext.Database.BeginTransactionAsync(cancellationToken));
    }

    public Task<ProjectMember?> GetMemberWithUserAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.ProjectMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public Task<List<ProjectTask>> GetAssignedTasksAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.ProjectTasks
            .Where(task => task.ProjectId == projectId && task.AssignedUserId == userId)
            .ToListAsync(cancellationToken);

    public void AddMember(ProjectMember member) => _dbContext.ProjectMembers.Add(member);

    public void RemoveMember(ProjectMember member) => _dbContext.ProjectMembers.Remove(member);

    public void AddActivity(ProjectActivity activity) => _dbContext.ProjectActivities.Add(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}