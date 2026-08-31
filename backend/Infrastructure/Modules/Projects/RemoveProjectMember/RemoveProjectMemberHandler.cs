using Application.Features.Projects;
using Application.Modules.Projects.RemoveProjectMember;
using Application.Modules.ProjectTasks.Assignments;
using Domain.Entities;

namespace Infrastructure.Modules.Projects.RemoveProjectMember;

/// <summary>
/// Coordinates member removal and task unassignment in one unit of work.
/// </summary>
public sealed class RemoveProjectMemberHandler : IRemoveProjectMemberHandler
{
    private readonly IRemoveProjectMemberStore _store;
    private readonly IProjectTaskMemberAssignmentWriter _taskAssignmentWriter;

    public RemoveProjectMemberHandler(
        IRemoveProjectMemberStore store,
        IProjectTaskMemberAssignmentWriter taskAssignmentWriter)
    {
        _store = store;
        _taskAssignmentWriter = taskAssignmentWriter;
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

        await _store.SaveChangesAsync(cancellationToken);

        return ProjectOperationResult<bool>.Success(true, "Project member removed");
    }
}
