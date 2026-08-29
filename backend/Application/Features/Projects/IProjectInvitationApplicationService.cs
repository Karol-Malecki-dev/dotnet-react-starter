namespace Application.Features.Projects;

/// <summary>
/// Handles project invitation use cases.
/// </summary>
public interface IProjectInvitationApplicationService
{
    Task<ProjectOperationResult<CreatedProjectInvitationView>> CreateProjectInvitationAsync(CreateProjectInvitationCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetProjectInvitationsAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetMyProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectInvitationView>> AcceptProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectInvitationView>> DeclineProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}