using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// Requests pending invitations addressed to the current user.
/// </summary>
public sealed record ListMyProjectInvitationsQuery(Guid UserId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>>;


/// <summary>
/// Provides the focused projection required by the list-my-project-invitations slice.
/// </summary>
public interface IListMyProjectInvitationsStore
{
    Task<IReadOnlyList<ProjectInvitationView>> QueryAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
