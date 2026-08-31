using Application.Features.Projects;
using Application.Modules.Projects.ListProjectInvitations;

namespace Infrastructure.Modules.Projects.ListProjectInvitations;

/// <summary>
/// Coordinates the owner check and project invitation projection.
/// </summary>
public sealed class ListProjectInvitationsHandler : IListProjectInvitationsHandler
{
    private readonly IListProjectInvitationsStore _store;

    public ListProjectInvitationsHandler(IListProjectInvitationsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> HandleAsync(
        ListProjectInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var invitations = await _store.QueryAsync(query.ProjectId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations);
    }
}
