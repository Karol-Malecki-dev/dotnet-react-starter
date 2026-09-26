using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ListProjectMembers;

namespace Infrastructure.Modules.Projects.ListProjectMembers;

/// <summary>
/// Coordinates project access checks and member-list projection.
/// </summary>
public sealed class ListProjectMembersHandler : IRequestHandler<ListProjectMembersQuery, ProjectOperationResult<IReadOnlyList<ProjectMemberView>>>
{
    private readonly IListProjectMembersStore _store;

    public ListProjectMembersHandler(IListProjectMembersStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectMemberView>>> Handle(
        ListProjectMembersQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.HasProjectAccessAsync(
                request.UserId,
                request.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectMemberView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var members = await _store.QueryAsync(request.ProjectId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectMemberView>>.Success(members.ToList());
    }
}
