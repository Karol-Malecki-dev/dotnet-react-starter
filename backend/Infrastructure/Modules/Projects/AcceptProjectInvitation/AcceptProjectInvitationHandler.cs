using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.AcceptProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Enums;
using Infrastructure.Modules.Projects.Invitations;

namespace Infrastructure.Modules.Projects.AcceptProjectInvitation;

/// <summary>
/// Handles acceptance of a project invitation.
/// </summary>
public sealed class AcceptProjectInvitationHandler : ProjectInvitationResponseHandlerBase, IRequestHandler<AcceptProjectInvitationCommand, ProjectOperationResult<ProjectInvitationView>>
{
    public AcceptProjectInvitationHandler(
        IProjectInvitationResponseStore store,
        IProjectInvitationNotificationWriter notificationWriter)
        : base(store, notificationWriter)
    {
    }

    public Task<ProjectOperationResult<ProjectInvitationView>> Handle(
        AcceptProjectInvitationCommand request,
        CancellationToken cancellationToken = default)
        => HandleResponseAsync(
            request.UserId,
            request.Token,
            ProjectInvitationStatus.Accepted,
            cancellationToken);
}
