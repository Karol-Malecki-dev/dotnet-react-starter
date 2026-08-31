using Application.Features.Projects;
using Application.Modules.Projects.DeclineProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Enums;
using Infrastructure.Modules.Projects.Invitations;

namespace Infrastructure.Modules.Projects.DeclineProjectInvitation;

/// <summary>
/// Handles rejection of a project invitation.
/// </summary>
public sealed class DeclineProjectInvitationHandler : ProjectInvitationResponseHandlerBase, IDeclineProjectInvitationHandler
{
    public DeclineProjectInvitationHandler(
        IProjectInvitationResponseStore store,
        IProjectInvitationNotificationWriter notificationWriter)
        : base(store, notificationWriter)
    {
    }

    public Task<ProjectOperationResult<ProjectInvitationView>> HandleAsync(
        DeclineProjectInvitationCommand command,
        CancellationToken cancellationToken = default)
        => HandleResponseAsync(
            command.UserId,
            command.Token,
            ProjectInvitationStatus.Declined,
            cancellationToken);
}
