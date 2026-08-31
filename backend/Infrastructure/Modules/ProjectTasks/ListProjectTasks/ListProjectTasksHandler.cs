using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.ListProjectTasks;

namespace Infrastructure.Modules.ProjectTasks.ListProjectTasks;

/// <summary>
/// Coordinates access checks and the read-only task list query.
/// </summary>
public sealed class ListProjectTasksHandler : IListProjectTasksHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IListProjectTasksQueryStore _queryStore;

    public ListProjectTasksHandler(
        IProjectTaskAccess projectTaskAccess,
        IListProjectTasksQueryStore queryStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _queryStore = queryStore;
    }

    public async Task<ProjectOperationResult<PagedProjectTaskView>> HandleAsync(
        ProjectTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        if (await _projectTaskAccess.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken) is null)
        {
            return ProjectOperationResult<PagedProjectTaskView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var page = await _queryStore.QueryAsync(query, cancellationToken);
        return ProjectOperationResult<PagedProjectTaskView>.Success(page);
    }
}
