using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.ProjectManagement.Tasks;

/// <summary>
/// EF Core implementation of state-changing ProjectTask use cases.
/// </summary>
public sealed class DatabaseProjectTaskCommandService : IProjectTaskCommandService
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskCommandStore _commandStore;
    private readonly INotificationService _notificationService;

    public DatabaseProjectTaskCommandService(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskCommandStore commandStore,
        INotificationService notificationService)
    {
        _projectTaskAccess = projectTaskAccess;
        _commandStore = commandStore;
        _notificationService = notificationService;
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> CreateProjectTaskAsync(CreateProjectTaskCommand command, CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(command.OwnerId, command.ProjectId, cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var assignedUserError = await ValidateAssignedUserAsync(command.ProjectId, command.AssignedUserId, cancellationToken);
        if (assignedUserError is not null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.ValidationError, assignedUserError);
        }
        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.Forbidden, "Viewer members cannot create tasks");
        }

        var task = ProjectTask.Create(command.ProjectId, command.Title, command.Description, command.Priority,
            command.DueDate, command.AssignedUserId, command.OwnerId, command.Labels);
        _commandStore.AddTask(task);
        AddActivity(task.ProjectId, command.OwnerId, "task.created", $"created the task '{task.Title}'.", task.Id);
        if (task.AssignedUserId.HasValue && task.AssignedUserId != command.OwnerId)
        {
            AddActivity(task.ProjectId, command.OwnerId, "task.assigned", $"assigned the task '{task.Title}'.", task.Id);
        }
        await _commandStore.SaveChangesAsync(cancellationToken);

        await NotifyAssigneeAsync(task, null, command.OwnerId, cancellationToken);
        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task created", 201);
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskAsync(UpdateProjectTaskCommand command, CancellationToken cancellationToken = default)
    {
        var taskResult = await GetEditableTaskAsync(command.OwnerId, command.ProjectId, command.TaskId, "You cannot edit this task", cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(taskResult.Status, taskResult.Message);
        }

        var assignedUserError = await ValidateAssignedUserAsync(command.ProjectId, command.AssignedUserId, cancellationToken);
        if (assignedUserError is not null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.ValidationError, assignedUserError);
        }

        var task = taskResult.Value!;
        var previousAssignedUserId = task.AssignedUserId;
        task.Rename(command.Title);
        task.ChangeDescription(command.Description);
        task.ChangePriority(command.Priority);
        task.SetDueDate(command.DueDate);
        if (command.AssignedUserId.HasValue) task.AssignTo(command.AssignedUserId.Value); else task.Unassign();

        var previousLabels = task.Labels.ToList();
        task.ReplaceLabels(command.Labels);
        _commandStore.ReplaceTaskLabels(task, previousLabels);
        if (task.AssignedUserId != previousAssignedUserId)
        {
            AddActivity(task.ProjectId, command.OwnerId, task.AssignedUserId.HasValue ? "task.assigned" : "task.unassigned",
                task.AssignedUserId.HasValue ? $"assigned the task '{task.Title}'." : $"unassigned the task '{task.Title}'.", task.Id);
        }
        await _commandStore.SaveChangesAsync(cancellationToken);

        await NotifyAssigneeAsync(task, previousAssignedUserId, command.OwnerId, cancellationToken);
        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task updated");
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskStatusAsync(UpdateProjectTaskStatusCommand command, CancellationToken cancellationToken = default)
    {
        var taskResult = await GetEditableTaskAsync(command.OwnerId, command.ProjectId, command.TaskId, "You cannot change this task status", cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(taskResult.Status, taskResult.Message);
        }

        var task = taskResult.Value!;
        var previousStatus = task.Status;
        task.ChangeStatus(command.Status);
        if (previousStatus != task.Status)
        {
            AddActivity(task.ProjectId, command.OwnerId, "task.status-changed", $"changed the status of '{task.Title}' to {task.Status}.", task.Id);
        }
        await _commandStore.SaveChangesAsync(cancellationToken);
        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task status updated");
    }

    public async Task<ProjectOperationResult<bool>> DeleteProjectTaskAsync(Guid userId, Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var taskResult = await GetEditableTaskAsync(userId, projectId, taskId, "You cannot delete this task", cancellationToken);
        if (!taskResult.IsSuccess)
        {
            return ProjectOperationResult<bool>.Failure(taskResult.Status, taskResult.Message);
        }

        _commandStore.RemoveTask(taskResult.Value!);
        await _commandStore.SaveChangesAsync(cancellationToken);
        return ProjectOperationResult<bool>.Success(true, "Project task deleted");
    }

    private async Task<ProjectOperationResult<ProjectTask>> GetEditableTaskAsync(Guid userId, Guid projectId, Guid taskId, string forbiddenMessage, CancellationToken cancellationToken)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(userId, projectId, cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTask>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(projectId, taskId, cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTask>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != userId))
        {
            return ProjectOperationResult<ProjectTask>.Failure(ProjectOperationStatus.Forbidden, forbiddenMessage);
        }

        return ProjectOperationResult<ProjectTask>.Success(task);
    }

    private async Task<string?> ValidateAssignedUserAsync(Guid projectId, Guid? assignedUserId, CancellationToken cancellationToken)
    {
        if (!assignedUserId.HasValue)
        {
            return null;
        }

        return await _commandStore.IsActiveProjectMemberAsync(projectId, assignedUserId.Value, cancellationToken)
            ? null
            : "Assigned user is not an active member of this project";
    }

    private async Task NotifyAssigneeAsync(ProjectTask task, Guid? previousAssignedUserId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!task.AssignedUserId.HasValue || task.AssignedUserId == previousAssignedUserId || task.AssignedUserId == actorUserId)
        {
            return;
        }

        await _notificationService.CreateAsync(task.AssignedUserId.Value, NotificationType.TaskAssigned,
            "You were assigned a task", $"You were assigned the task '{task.Title}'.", "ProjectTask", task.Id, task.ProjectId,
            cancellationToken: cancellationToken);
    }

    private void AddActivity(Guid projectId, Guid actorUserId, string type, string description, Guid projectTaskId)
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
        task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority,
        task.DueDate, task.AssignedUserId, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt,
        task.Labels.OrderBy(label => label.Name).Select(label => label.Name).ToList());
}