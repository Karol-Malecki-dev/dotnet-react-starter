using Application.Features.Projects;
using Application.Modules.Projects.RemoveProjectMember;
using Application.Modules.ProjectTasks.Assignments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.RemoveProjectMember;

/// <summary>
/// Coordinates member removal and task unassignment in one unit of work.
/// </summary>
public sealed class RemoveProjectMemberHandler : IRemoveProjectMemberHandler
{
    private readonly IRemoveProjectMemberStore _store;
    private readonly IProjectTaskMemberAssignmentWriter _taskAssignmentWriter;
    private readonly ICollaborationNotificationWriter? _notificationWriter;

    public RemoveProjectMemberHandler(
        IRemoveProjectMemberStore store,
        IProjectTaskMemberAssignmentWriter taskAssignmentWriter,
        ICollaborationNotificationWriter? notificationWriter = null)
    {
        _store = store;
        _taskAssignmentWriter = taskAssignmentWriter;
        _notificationWriter = notificationWriter;
    }

    public async Task<ProjectOperationResult<bool>> HandleAsync(
        RemoveProjectMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectWithMembersAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (command.UserId == command.OwnerId)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Conflict,
                "Project owner cannot be removed");
        }

        var member = project.Members.FirstOrDefault(candidate => candidate.UserId == command.UserId);
        if (member is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project member not found");
        }

        await _taskAssignmentWriter.UnassignAllAsync(
            command.ProjectId,
            command.UserId,
            cancellationToken);

        project.RemoveMember(command.UserId);
        _store.RemoveMember(member);
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = command.ProjectId,
            ActorUserId = command.OwnerId,
            Type = "member.removed",
            Description = "removed a project member."
        });

        if (_notificationWriter is not null)
        {
            await _notificationWriter.StageAsync(
                command.UserId,
                NotificationType.ProjectMemberRemoved,
                "Removed from project",
                $"You were removed from '{project.Name}'.",
                "project",
                project.Id,
                project.Id,
                $"project:{project.Id}:member:{command.UserId}:removed",
                cancellationToken);
        }

        await _store.SaveChangesAsync(cancellationToken);

        return ProjectOperationResult<bool>.Success(true, "Project member removed");
    }
}
