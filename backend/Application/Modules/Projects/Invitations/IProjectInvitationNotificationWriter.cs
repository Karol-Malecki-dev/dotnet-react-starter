using Domain.Enums;

namespace Application.Modules.Projects.Invitations;

/// <summary>
/// Stages notifications produced by project-invitation use cases.
/// Implementations must participate in the current unit of work and must not commit it.
/// </summary>
public interface IProjectInvitationNotificationWriter
{
    Task AddInvitationCreatedNotificationAsync(
        Guid recipientUserId,
        Guid projectId,
        Guid invitationId,
        string projectName,
        string inviterDisplayName,
        CancellationToken cancellationToken = default);

    Task AddInvitationResponseNotificationAsync(
        Guid ownerUserId,
        Guid projectId,
        Guid invitationId,
        string projectName,
        string recipientDisplayName,
        ProjectInvitationStatus status,
        CancellationToken cancellationToken = default);
}
