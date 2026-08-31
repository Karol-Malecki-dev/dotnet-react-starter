using Application.Features.Projects;

namespace Application.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// Requests active users who can be added to a project owned by the current user.
/// </summary>
public sealed record ListAvailableProjectMembersQuery(Guid OwnerId, Guid ProjectId);

/// <summary>
/// Executes the list-available-project-members use case.
/// </summary>
public interface IListAvailableProjectMembersHandler
{
    Task<ProjectOperationResult<List<ProjectMemberUserView>>> HandleAsync(
        ListAvailableProjectMembersQuery query,
        CancellationToken cancellationToken = default);
}

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
