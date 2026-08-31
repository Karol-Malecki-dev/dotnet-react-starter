namespace Application.Modules.ProjectTasks.ListProjectTasks;

/// <summary>
/// Provides the persistence query required by the list-project-tasks use case.
/// </summary>
public interface IListProjectTasksQueryStore
{
    Task<PagedProjectTaskView> QueryAsync(
        ProjectTaskQuery query,
        CancellationToken cancellationToken = default);
}
