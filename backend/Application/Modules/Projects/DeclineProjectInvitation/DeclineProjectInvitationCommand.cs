using Application.Features.Projects;

namespace Application.Modules.Projects.DeclineProjectInvitation;

/// <summary>
/// Declines an invitation addressed to the current user.
/// </summary>
public sealed record DeclineProjectInvitationCommand(Guid UserId, string Token);

/// <summary>
/// Executes the decline-project-invitation use case.
/// </summary>
public interface IDeclineProjectInvitationHandler
{
    Task<ProjectOperationResult<ProjectInvitationView>> HandleAsync(
        DeclineProjectInvitationCommand command,
        CancellationToken cancellationToken = default);
}
