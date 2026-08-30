using Application.Features.Projects;
using Application.Modules.Projects.ListProjects;

namespace Infrastructure.Modules.Projects.ListProjects;

/// <summary>
/// Coordinates the list-projects query without exposing persistence details to the API.
/// </summary>
public sealed class ListProjectsHandler : IListProjectsHandler
{
    private readonly IListProjectsStore _store;

    public ListProjectsHandler(IListProjectsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectView>>> HandleAsync(
        ListProjectsQuery query,
        CancellationToken cancellationToken = default)
    {
        var projects = await _store.QueryAsync(query, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectView>>.Success(projects);
    }
}
