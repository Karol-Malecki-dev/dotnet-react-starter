using Application.Features.Projects;

namespace Application.Modules.Projects.ListProjectMembers;

/// <summary>
/// Requests the active members of a project visible to the current user.
/// </summary>
public sealed record ListProjectMembersQuery(Guid UserId, Guid ProjectId);

/// <summary>
/// Executes the list-project-members use case.
/// </summary>
public interface IListProjectMembersHandler
{
    Task<ProjectOperationResult<List<ProjectMemberView>>> HandleAsync(
        ListProjectMembersQuery query,
        CancellationToken cancellationToken = default);
}

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
