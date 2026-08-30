using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.DeleteProjectTask;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.DeleteProjectTask;

/// <summary>
/// Coordinates authorization, concurrency validation, and persistence for task deletion.
/// </summary>
public sealed class DeleteProjectTaskHandler : IDeleteProjectTaskHandler
{
    private const string ConcurrencyConflictMessage = "Project task was modified concurrently; refresh and retry";
    private const string ConcurrencyStampRequiredMessage = "Project task concurrency stamp is required";

    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskCommandStore _commandStore;

    public DeleteProjectTaskHandler(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskCommandStore commandStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _commandStore = commandStore;
    }

    public async Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var taskResult = await GetEditableTaskAsync(
            command.UserId,
            command.ProjectId,
            command.TaskId,
            cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return ProjectOperationResult<bool>.Failure(
                taskResult.Status,
                taskResult.Message);
        }

        var task = taskResult.Value!;
        if (string.IsNullOrWhiteSpace(command.ExpectedConcurrencyStamp))
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.ValidationError,
                ConcurrencyStampRequiredMessage);
        }

        if (!string.Equals(task.ConcurrencyStamp, command.ExpectedConcurrencyStamp, StringComparison.Ordinal))
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        _commandStore.RemoveTask(task);
        try
        {
            await _commandStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _commandStore.ClearChangeTracker();
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        return ProjectOperationResult<bool>.Success(true, "Project task deleted");
    }

    private async Task<ProjectOperationResult<ProjectTask>> GetEditableTaskAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            userId,
            projectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTask>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            projectId,
            taskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTask>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        if (role == ProjectMemberRole.Viewer
            || (role == ProjectMemberRole.Member && task.CreatedByUserId != userId))
        {
            return ProjectOperationResult<ProjectTask>.Failure(
                ProjectOperationStatus.Forbidden,
                "You cannot delete this task");
        }

        return ProjectOperationResult<ProjectTask>.Success(task);
    }
}
