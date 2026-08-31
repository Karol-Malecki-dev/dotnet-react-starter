using Application.Features.Projects;
using Application.Modules.Projects.ListAvailableProjectMembers;

namespace Infrastructure.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// Coordinates the owner check and available-member projection.
/// </summary>
public sealed class ListAvailableProjectMembersHandler : IListAvailableProjectMembersHandler
{
    private readonly IListAvailableProjectMembersStore _store;

    public ListAvailableProjectMembersHandler(IListAvailableProjectMembersStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<List<ProjectMemberUserView>>> HandleAsync(
        ListAvailableProjectMembersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<List<ProjectMemberUserView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var users = await _store.QueryAsync(query.ProjectId, cancellationToken);
        return ProjectOperationResult<List<ProjectMemberUserView>>.Success(users.ToList());
    }
}
