using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Domain.Entities;

namespace Infrastructure.ProjectManagement.Tasks;

/// <summary>
/// EF Core implementation of read-only ProjectTask use cases.
/// </summary>
public sealed class DatabaseProjectTaskQueryService : IProjectTaskQueryService
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskQueryStore _queryStore;

    public DatabaseProjectTaskQueryService(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskQueryStore queryStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _queryStore = queryStore;
    }

    public async Task<ProjectOperationResult<PagedProjectTaskView>> GetProjectTasksAsync(ProjectTaskQuery query)
    {
        if (await _projectTaskAccess.GetActiveProjectRoleAsync(query.UserId, query.ProjectId) is null)
        {
            return ProjectOperationResult<PagedProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var page = await _queryStore.QueryAsync(query);
        return ProjectOperationResult<PagedProjectTaskView>.Success(page);
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> GetProjectTaskAsync(Guid userId, Guid projectId, Guid taskId)
    {
        if (await _projectTaskAccess.GetActiveProjectRoleAsync(userId, projectId) is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(projectId, taskId);
        return task is null
            ? ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project task not found")
            : ProjectOperationResult<ProjectTaskView>.Success(MapToView(task));
    }

    private static ProjectTaskView MapToView(ProjectTask task) => new(
        task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority,
        task.DueDate, task.AssignedUserId, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt,
        task.Labels.OrderBy(label => label.Name).Select(label => label.Name).ToList());
}