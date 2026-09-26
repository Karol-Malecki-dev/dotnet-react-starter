using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ListProjectInvitations;

namespace Infrastructure.Modules.Projects.ListProjectInvitations;

/// <summary>
/// Coordinates the owner check and project invitation projection.
/// </summary>
public sealed class ListProjectInvitationsHandler : IRequestHandler<ListProjectInvitationsQuery, ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>>
{
    private readonly IListProjectInvitationsStore _store;

    public ListProjectInvitationsHandler(IListProjectInvitationsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> Handle(
        ListProjectInvitationsQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.OwnedProjectExistsAsync(
                request.OwnerId,
                request.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var invitations = await _store.QueryAsync(request.ProjectId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations);
    }
}
