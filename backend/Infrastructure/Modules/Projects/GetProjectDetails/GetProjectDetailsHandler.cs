using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDetails;
using MediatR;

namespace Infrastructure.Modules.Projects.GetProjectDetails;

/// <summary>
/// Coordinates the project-details query and maps inaccessible projects to not found.
/// </summary>
public sealed class GetProjectDetailsHandler
    : IRequestHandler<GetProjectDetailsQuery, ProjectOperationResult<ProjectView>>
{
    private readonly IGetProjectDetailsStore _store;

    public GetProjectDetailsHandler(IGetProjectDetailsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<ProjectView>> Handle(
        GetProjectDetailsQuery request,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.QueryAsync(
            request.UserId,
            request.ProjectId,
            request.IncludeArchived,
            cancellationToken);

        return project is null
            ? ProjectOperationResult<ProjectView>.Failure(ProjectOperationStatus.NotFound, "Project not found")
            : ProjectOperationResult<ProjectView>.Success(project);
    }
}
