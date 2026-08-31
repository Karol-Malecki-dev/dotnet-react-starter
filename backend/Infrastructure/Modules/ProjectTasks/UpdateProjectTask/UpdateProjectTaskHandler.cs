using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Application.Modules.ProjectTasks.UpdateProjectTask;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.UpdateProjectTask;

/// <summary>
/// Coordinates authorization, validation, mutation, concurrency handling, and notification for task updates.
/// </summary>
public sealed class UpdateProjectTaskHandler : IUpdateProjectTaskHandler
{
    private const string ConcurrencyConflictMessage = "Project task was modified concurrently; refresh and retry";
    private const string ConcurrencyStampRequiredMessage = "Project task concurrency stamp is required";

    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskCommandStore _commandStore;
    private readonly IProjectTaskAssignmentNotificationWriter _assignmentNotificationWriter;

    public UpdateProjectTaskHandler(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskCommandStore commandStore,
        IProjectTaskAssignmentNotificationWriter assignmentNotificationWriter)
    {
        _projectTaskAccess = projectTaskAccess;
        _commandStore = commandStore;
        _assignmentNotificationWriter = assignmentNotificationWriter;
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        UpdateProjectTaskCommand command,
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

        var assignedUserError = await ValidateAssignedUserAsync(
            command.ProjectId,
            command.AssignedUserId,
            cancellationToken);
        if (assignedUserError is not null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.ValidationError,
                assignedUserError);
        }

        var previousAssignedUserId = task.AssignedUserId;
        task.Rename(command.Title);
        task.ChangeDescription(command.Description);
        task.ChangePriority(command.Priority);
        task.SetDueDate(command.DueDate);
        if (command.AssignedUserId.HasValue)
        {
            task.AssignTo(command.AssignedUserId.Value);
        }
        else
        {
            task.Unassign();
        }

        var previousLabels = task.Labels.ToList();
        task.ReplaceLabels(command.Labels);
        _commandStore.ReplaceTaskLabels(task, previousLabels);

        if (task.AssignedUserId != previousAssignedUserId)
        {
            AddActivity(
                task.ProjectId,
                command.UserId,
                task.AssignedUserId.HasValue ? "task.assigned" : "task.unassigned",
                task.AssignedUserId.HasValue
                    ? $"assigned the task '{task.Title}'."
                    : $"unassigned the task '{task.Title}'.",
                task.Id);
        }

        try
        {
            await PrepareAssigneeNotificationAsync(
                task,
                previousAssignedUserId,
                command.UserId,
                cancellationToken);

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
            "Project task updated");
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
                "You cannot edit this task");
        }

        return ProjectOperationResult<ProjectTask>.Success(task);
    }

    private async Task<string?> ValidateAssignedUserAsync(
        Guid projectId,
        Guid? assignedUserId,
        CancellationToken cancellationToken)
    {
        if (!assignedUserId.HasValue)
        {
            return null;
        }

        return await _commandStore.IsActiveProjectMemberAsync(
            projectId,
            assignedUserId.Value,
            cancellationToken)
            ? null
            : "Assigned user is not an active member of this project";
    }

    private async Task PrepareAssigneeNotificationAsync(
        ProjectTask task,
        Guid? previousAssignedUserId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!task.AssignedUserId.HasValue
            || task.AssignedUserId == previousAssignedUserId
            || task.AssignedUserId == actorUserId)
        {
            return;
        }

        await _assignmentNotificationWriter.AddTaskAssignedNotificationAsync(
            task.AssignedUserId.Value,
            task.ProjectId,
            task.Id,
            task.Title,
            cancellationToken);
    }

    private void AddActivity(
        Guid projectId,
        Guid actorUserId,
        string type,
        string description,
        Guid projectTaskId)
    {
        _commandStore.AddActivity(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = actorUserId,
            Type = type,
            Description = description,
            ProjectTaskId = projectTaskId
        });
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
