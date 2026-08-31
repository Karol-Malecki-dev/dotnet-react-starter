using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.UpdateProjectTaskStatus;

/// <summary>
/// Coordinates authorization, status mutation, activity recording, and concurrency handling.
/// </summary>
public sealed class UpdateProjectTaskStatusHandler : IUpdateProjectTaskStatusHandler
{
    private const string ConcurrencyConflictMessage = "Project task was modified concurrently; refresh and retry";
    private const string ConcurrencyStampRequiredMessage = "Project task concurrency stamp is required";

    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskCommandStore _commandStore;
    private readonly ICollaborationNotificationWriter? _notificationWriter;

    public UpdateProjectTaskStatusHandler(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskCommandStore commandStore,
        ICollaborationNotificationWriter? notificationWriter = null)
    {
        _projectTaskAccess = projectTaskAccess;
        _commandStore = commandStore;
        _notificationWriter = notificationWriter;
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        UpdateProjectTaskStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var taskResult = await GetEditableTaskAsync(
            command.UserId,
            command.ProjectId,
            command.TaskId,
            cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                taskResult.Status,
                taskResult.Message);
        }

        var task = taskResult.Value!;
        if (string.IsNullOrWhiteSpace(command.ExpectedConcurrencyStamp))
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.ValidationError,
                ConcurrencyStampRequiredMessage);
        }

        if (!string.Equals(task.ConcurrencyStamp, command.ExpectedConcurrencyStamp, StringComparison.Ordinal))
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        var previousStatus = task.Status;
        task.ChangeStatus(command.Status);
        if (previousStatus != task.Status)
        {
            _commandStore.AddActivity(new ProjectActivity
            {
                ProjectId = task.ProjectId,
                ActorUserId = command.UserId,
                Type = "task.status-changed",
                Description = $"changed the status of '{task.Title}' to {task.Status}.",
                ProjectTaskId = task.Id
            });

            if (_notificationWriter is not null && task.AssignedUserId is { } assigneeId && assigneeId != command.UserId)
            {
                await _notificationWriter.StageAsync(
                    assigneeId,
                    NotificationType.TaskStatusChanged,
                    "Task status changed",
                    $"'{task.Title}' changed to {task.Status}.",
                    "projectTask",
                    task.Id,
                    task.ProjectId,
                    $"task:{task.Id}:status:{task.Status}:version:{task.ConcurrencyStamp}",
                    cancellationToken);
            }
        }

        try
        {
            await _commandStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _commandStore.ClearChangeTracker();
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        return ProjectOperationResult<ProjectTaskView>.Success(
            MapToView(task),
            "Project task status updated");
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
                "You cannot change this task status");
        }

        return ProjectOperationResult<ProjectTask>.Success(task);
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
