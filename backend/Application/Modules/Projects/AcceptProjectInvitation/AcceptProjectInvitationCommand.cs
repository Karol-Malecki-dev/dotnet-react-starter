using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.AcceptProjectInvitation;

/// <summary>
/// Accepts an invitation addressed to the current user.
/// </summary>
public sealed record AcceptProjectInvitationCommand(Guid UserId, string Token)
    : IRequest<ProjectOperationResult<ProjectInvitationView>>;

