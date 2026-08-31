using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.GetProjectTaskDetails;
using Domain.Entities;

namespace Infrastructure.Modules.ProjectTasks.GetProjectTaskDetails;

/// <summary>
/// Coordinates authorization and task retrieval for the project-task details slice.
/// </summary>
public sealed class GetProjectTaskDetailsHandler : IGetProjectTaskDetailsHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;

    public GetProjectTaskDetailsHandler(IProjectTaskAccess projectTaskAccess)
    {
        _projectTaskAccess = projectTaskAccess;
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        GetProjectTaskDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (await _projectTaskAccess.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken) is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            query.ProjectId,
            query.TaskId,
            cancellationToken);

        return task is null
            ? ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found")
            : ProjectOperationResult<ProjectTaskView>.Success(MapToView(task));
    }

    private static ProjectTaskView MapToView(ProjectTask task) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate,
        task.AssignedUserId,
        task.CreatedByUserId,
        task.CreatedAt,
        task.UpdatedAt,
        task.ConcurrencyStamp,
        task.Labels.OrderBy(label => label.Name).Select(label => label.Name).ToList());
}
