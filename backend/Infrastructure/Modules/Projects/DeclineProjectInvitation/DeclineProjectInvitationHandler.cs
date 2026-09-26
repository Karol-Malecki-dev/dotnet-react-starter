using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.DeclineProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Enums;
using Infrastructure.Modules.Projects.Invitations;

namespace Infrastructure.Modules.Projects.DeclineProjectInvitation;

/// <summary>
/// Handles rejection of a project invitation.
/// </summary>
public sealed class DeclineProjectInvitationHandler : ProjectInvitationResponseHandlerBase, IRequestHandler<DeclineProjectInvitationCommand, ProjectOperationResult<ProjectInvitationView>>
{
    public DeclineProjectInvitationHandler(
        IProjectInvitationResponseStore store,
        IProjectInvitationNotificationWriter notificationWriter)
        : base(store, notificationWriter)
    {
    }

    public Task<ProjectOperationResult<ProjectInvitationView>> Handle(
        DeclineProjectInvitationCommand request,
        CancellationToken cancellationToken = default)
        => HandleResponseAsync(
            request.UserId,
            request.Token,
            ProjectInvitationStatus.Declined,
            cancellationToken);
}
