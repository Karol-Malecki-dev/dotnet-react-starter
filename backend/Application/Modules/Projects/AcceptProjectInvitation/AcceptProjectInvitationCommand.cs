using Application.Features.Projects;

namespace Application.Modules.Projects.AcceptProjectInvitation;

/// <summary>
/// Accepts an invitation addressed to the current user.
/// </summary>
public sealed record AcceptProjectInvitationCommand(Guid UserId, string Token);

/// <summary>
/// Executes the accept-project-invitation use case.
/// </summary>
public interface IAcceptProjectInvitationHandler
{
    Task<ProjectOperationResult<ProjectInvitationView>> HandleAsync(
        AcceptProjectInvitationCommand command,
        CancellationToken cancellationToken = default);
}
