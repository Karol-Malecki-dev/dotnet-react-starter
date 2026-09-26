using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.RemoveProjectMember;
using Application.Modules.ProjectTasks.Assignments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.RemoveProjectMember;

/// <summary>
/// Coordinates member removal and task unassignment in one unit of work.
/// </summary>
public sealed class RemoveProjectMemberHandler : IRequestHandler<RemoveProjectMemberCommand, ProjectOperationResult<bool>>
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

    public async Task<ProjectOperationResult<bool>> Handle(
        RemoveProjectMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectWithMembersAsync(
            request.OwnerId,
            request.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (request.UserId == request.OwnerId)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Conflict,
                "Project owner cannot be removed");
        }

        var member = project.Members.FirstOrDefault(candidate => candidate.UserId == request.UserId);
        if (member is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project member not found");
        }

        await _taskAssignmentWriter.UnassignAllAsync(
            request.ProjectId,
            request.UserId,
            cancellationToken);

        project.RemoveMember(request.UserId);
        _store.RemoveMember(member);
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = request.ProjectId,
            ActorUserId = request.OwnerId,
            Type = "member.removed",
            Description = "removed a project member."
        });

        if (_notificationWriter is not null)
        {
            await _notificationWriter.StageAsync(
                request.UserId,
                NotificationType.ProjectMemberRemoved,
                "Removed from project",
                $"You were removed from '{project.Name}'.",
                "project",
                project.Id,
                project.Id,
                $"project:{project.Id}:member:{request.UserId}:removed",
                cancellationToken);
        }

        await _store.SaveChangesAsync(cancellationToken);

        return ProjectOperationResult<bool>.Success(true, "Project member removed");
    }
}
