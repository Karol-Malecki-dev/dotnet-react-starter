using Domain.Entities;

namespace Application.Features.Projects;

/// <summary>
/// Provides persistence operations required by project membership use cases.
/// </summary>
public interface IProjectMembershipStore
{
    Task<bool> OwnedProjectExistsAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMemberView>> GetMembersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMemberUserView>> GetAvailableUsersAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<User?> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectMember?> GetMemberWithUserAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<ProjectTask>> GetAssignedTasksAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    void AddMember(ProjectMember member);
    void RemoveMember(ProjectMember member);
    void AddActivity(ProjectActivity activity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}