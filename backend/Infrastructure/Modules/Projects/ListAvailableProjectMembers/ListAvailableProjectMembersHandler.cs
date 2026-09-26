using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ListAvailableProjectMembers;

namespace Infrastructure.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// Coordinates the owner check and available-member projection.
/// </summary>
public sealed class ListAvailableProjectMembersHandler : IRequestHandler<ListAvailableProjectMembersQuery, ProjectOperationResult<IReadOnlyList<ProjectMemberUserView>>>
{
    private readonly IListAvailableProjectMembersStore _store;

    public ListAvailableProjectMembersHandler(IListAvailableProjectMembersStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectMemberUserView>>> Handle(
        ListAvailableProjectMembersQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.OwnedProjectExistsAsync(
                request.OwnerId,
                request.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectMemberUserView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var users = await _store.QueryAsync(request.ProjectId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectMemberUserView>>.Success(users.ToList());
    }
}
