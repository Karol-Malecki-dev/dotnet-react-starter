using Application.Features.Projects;

namespace Application.Modules.Projects.ListProjectInvitations;

/// <summary>
/// Requests invitations created for a project owned by the current user.
/// </summary>
public sealed record ListProjectInvitationsQuery(Guid OwnerId, Guid ProjectId);

/// <summary>
/// Executes the list-project-invitations use case.
/// </summary>
public interface IListProjectInvitationsHandler
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> HandleAsync(
        ListProjectInvitationsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused projection required by the list-project-invitations slice.
/// </summary>
public interface IListProjectInvitationsStore
{
    Task<bool> OwnedProjectExistsAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectInvitationView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
