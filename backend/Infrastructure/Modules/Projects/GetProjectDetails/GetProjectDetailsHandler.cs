using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDetails;

namespace Infrastructure.Modules.Projects.GetProjectDetails;

/// <summary>
/// Coordinates the project-details query and maps inaccessible projects to not found.
/// </summary>
public sealed class GetProjectDetailsHandler : IGetProjectDetailsHandler
{
    private readonly IGetProjectDetailsStore _store;

    public GetProjectDetailsHandler(IGetProjectDetailsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<ProjectView>> HandleAsync(
        GetProjectDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.QueryAsync(
            query.UserId,
            query.ProjectId,
            query.IncludeArchived,
            cancellationToken);

        return project is null
            ? ProjectOperationResult<ProjectView>.Failure(ProjectOperationStatus.NotFound, "Project not found")
            : ProjectOperationResult<ProjectView>.Success(project);
    }
}
