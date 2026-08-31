using Application.Features.Projects;
using Application.Modules.Projects.AcceptProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Enums;
using Infrastructure.Modules.Projects.Invitations;

namespace Infrastructure.Modules.Projects.AcceptProjectInvitation;

/// <summary>
/// Handles acceptance of a project invitation.
/// </summary>
public sealed class AcceptProjectInvitationHandler : ProjectInvitationResponseHandlerBase, IAcceptProjectInvitationHandler
{
    public AcceptProjectInvitationHandler(
        IProjectInvitationResponseStore store,
        IProjectInvitationNotificationWriter notificationWriter)
        : base(store, notificationWriter)
    {
    }

    public Task<ProjectOperationResult<ProjectInvitationView>> HandleAsync(
        AcceptProjectInvitationCommand command,
        CancellationToken cancellationToken = default)
        => HandleResponseAsync(
            command.UserId,
            command.Token,
            ProjectInvitationStatus.Accepted,
            cancellationToken);
}
