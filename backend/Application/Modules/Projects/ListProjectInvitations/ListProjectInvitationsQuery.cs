using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.ListProjectInvitations;

/// <summary>
/// Requests invitations created for a project owned by the current user.
/// </summary>
public sealed record ListProjectInvitationsQuery(Guid OwnerId, Guid ProjectId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>>;


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
