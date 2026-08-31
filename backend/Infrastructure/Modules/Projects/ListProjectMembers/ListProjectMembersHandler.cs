using Application.Features.Projects;
using Application.Modules.Projects.ListProjectMembers;

namespace Infrastructure.Modules.Projects.ListProjectMembers;

/// <summary>
/// Coordinates project access checks and member-list projection.
/// </summary>
public sealed class ListProjectMembersHandler : IListProjectMembersHandler
{
    private readonly IListProjectMembersStore _store;

    public ListProjectMembersHandler(IListProjectMembersStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<List<ProjectMemberView>>> HandleAsync(
        ListProjectMembersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<List<ProjectMemberView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var members = await _store.QueryAsync(query.ProjectId, cancellationToken);
        return ProjectOperationResult<List<ProjectMemberView>>.Success(members.ToList());
    }
}
