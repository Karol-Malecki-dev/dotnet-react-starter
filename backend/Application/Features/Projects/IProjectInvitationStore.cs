using Domain.Entities;

namespace Application.Features.Projects;

/// <summary>
/// Provides persistence operations required by project invitation use cases.
/// </summary>
public interface IProjectInvitationStore
{
    Task<User?> GetActiveUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingInvitationAsync(Guid projectId, Guid userId, DateTime now, CancellationToken cancellationToken = default);
    Task<string> GetProjectNameAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<string> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectInvitation>> GetProjectInvitationsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectInvitation>> GetUserPendingInvitationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectInvitation?> GetInvitationWithDetailsAsync(string tokenHash, CancellationToken cancellationToken = default);
    void AddInvitation(ProjectInvitation invitation);
    void AddMember(ProjectMember member);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}