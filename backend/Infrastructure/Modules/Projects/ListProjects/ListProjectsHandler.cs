using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ListProjects;

namespace Infrastructure.Modules.Projects.ListProjects;

/// <summary>
/// Coordinates the list-projects request without exposing persistence details to the API.
/// </summary>
public sealed class ListProjectsHandler : IRequestHandler<ListProjectsQuery, ProjectOperationResult<IReadOnlyList<ProjectView>>>
{
    private readonly IListProjectsStore _store;

    public ListProjectsHandler(IListProjectsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectView>>> Handle(
        ListProjectsQuery request,
        CancellationToken cancellationToken = default)
    {
        var projects = await _store.QueryAsync(request, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectView>>.Success(projects);
    }
}
