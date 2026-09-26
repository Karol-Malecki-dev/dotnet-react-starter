using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.ListProjectMembers;

/// <summary>
/// Requests the active members of a project visible to the current user.
/// </summary>
public sealed record ListProjectMembersQuery(Guid UserId, Guid ProjectId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectMemberView>>>;


/// <summary>
/// Provides the focused persistence operations required by the list-project-members slice.
/// </summary>
public interface IListProjectMembersStore
{
    Task<bool> HasProjectAccessAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectMemberView>> QueryAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
