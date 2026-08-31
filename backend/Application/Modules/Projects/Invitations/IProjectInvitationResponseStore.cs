using Domain.Entities;

namespace Application.Modules.Projects.Invitations;

/// <summary>
/// Provides the shared persistence operations required by invitation response slices.
/// </summary>
public interface IProjectInvitationResponseStore
{
    Task<ProjectInvitation?> GetByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    void AddMember(ProjectMember member);

    void AddActivity(ProjectActivity activity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
