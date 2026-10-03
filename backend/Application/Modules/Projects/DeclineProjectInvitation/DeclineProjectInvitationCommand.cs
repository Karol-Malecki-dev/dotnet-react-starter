using Application.Modules.Projects.Shared;
using MediatR;

namespace Application.Modules.Projects.DeclineProjectInvitation;

/// <summary>
/// Declines an invitation addressed to the current user.
/// </summary>
public sealed record DeclineProjectInvitationCommand(Guid UserId, string Token)
    : IRequest<ProjectOperationResult<ProjectInvitationView>>;

