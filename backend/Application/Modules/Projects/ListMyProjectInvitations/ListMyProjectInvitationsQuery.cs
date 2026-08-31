using Application.Features.Projects;

namespace Application.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// Requests pending invitations addressed to the current user.
/// </summary>
public sealed record ListMyProjectInvitationsQuery(Guid UserId);

/// <summary>
/// Executes the list-my-project-invitations use case.
/// </summary>
public interface IListMyProjectInvitationsHandler
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> HandleAsync(
        ListMyProjectInvitationsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused projection required by the list-my-project-invitations slice.
/// </summary>
public interface IListMyProjectInvitationsStore
{
    Task<IReadOnlyList<ProjectInvitationView>> QueryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
