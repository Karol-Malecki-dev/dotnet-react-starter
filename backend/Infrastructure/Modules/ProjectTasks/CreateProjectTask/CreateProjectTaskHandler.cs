using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Interfaces;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTask;

/// <summary>
/// Coordinates authorization, task creation, activity recording, and assignee notification.
/// </summary>
public sealed class CreateProjectTaskHandler : ICreateProjectTaskHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IProjectTaskCommandStore _commandStore;
    private readonly INotificationService _notificationService;

    public CreateProjectTaskHandler(
        IProjectTaskAccess projectTaskAccess,
        IProjectTaskCommandStore commandStore,
        INotificationService notificationService)
    {
        _projectTaskAccess = projectTaskAccess;
        _commandStore = commandStore;
        _notificationService = notificationService;
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        CreateProjectTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);

        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
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

        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(
                ProjectOperationStatus.Forbidden,
                "Viewer members cannot create tasks");
        }

        var task = ProjectTask.Create(
            command.ProjectId,
            command.Title,
            command.Description,
            command.Priority,
            command.DueDate,
            command.AssignedUserId,
            command.OwnerId,
            command.Labels);

        _commandStore.AddTask(task);
        AddActivity(
            task.ProjectId,
            command.OwnerId,
            "task.created",
            $"created the task '{task.Title}'.",
            task.Id);

        if (task.AssignedUserId.HasValue && task.AssignedUserId != command.OwnerId)
        {
            AddActivity(
                task.ProjectId,
                command.OwnerId,
                "task.assigned",
                $"assigned the task '{task.Title}'.",
                task.Id);
        }

        await _commandStore.SaveChangesAsync(cancellationToken);
        await NotifyAssigneeAsync(task, command.OwnerId, cancellationToken);

        return ProjectOperationResult<ProjectTaskView>.Success(
            MapToView(task),
            "Project task created",
            201);
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

    private async Task NotifyAssigneeAsync(
        ProjectTask task,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!task.AssignedUserId.HasValue || task.AssignedUserId == actorUserId)
        {
            return;
        }

        await _notificationService.CreateAsync(
            task.AssignedUserId.Value,
            NotificationType.TaskAssigned,
            "You were assigned a task",
            $"You were assigned the task '{task.Title}'.",
            "ProjectTask",
            task.Id,
            task.ProjectId,
            cancellationToken: cancellationToken);
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
