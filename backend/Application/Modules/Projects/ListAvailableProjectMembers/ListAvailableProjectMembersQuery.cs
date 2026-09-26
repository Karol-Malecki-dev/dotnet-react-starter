using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// Requests active users who can be added to a project owned by the current user.
/// </summary>
public sealed record ListAvailableProjectMembersQuery(Guid OwnerId, Guid ProjectId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectMemberUserView>>>;


/// <summary>
/// Provides the focused persistence projection required by the list-available-project-members slice.
/// </summary>
public interface IListAvailableProjectMembersStore
{
    Task<bool> OwnedProjectExistsAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMemberUserView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
