using Application.Features.Projects;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// Coordinates project-member role changes through the project aggregate.
/// </summary>
public sealed class ChangeProjectMemberRoleHandler : IChangeProjectMemberRoleHandler
{
    private readonly IChangeProjectMemberRoleStore _store;
    private readonly ICollaborationNotificationWriter? _notificationWriter;

    public ChangeProjectMemberRoleHandler(
        IChangeProjectMemberRoleStore store,
        ICollaborationNotificationWriter? notificationWriter = null)
    {
        _store = store;
        _notificationWriter = notificationWriter;
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> HandleAsync(
        ChangeProjectMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectWithMembersAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (command.UserId == command.OwnerId || command.Role == ProjectMemberRole.Owner)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "The project owner role cannot be changed");
        }

        if (command.Role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Invalid project member role");
        }

        if (!project.Members.Any(member => member.UserId == command.UserId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project member not found");
        }

        ProjectMember member;
        try
        {
            member = project.ChangeMemberRole(command.UserId, command.Role);
        }
        catch (InvalidOperationException)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "The project member role cannot be changed");
        }

        if (_notificationWriter is not null)
        {
            await _notificationWriter.StageAsync(
                command.UserId,
                NotificationType.ProjectMemberRoleChanged,
                "Project role changed",
                $"Your role in '{project.Name}' changed to {member.Role}.",
                "project",
                project.Id,
                project.Id,
                $"project:{project.Id}:member:{command.UserId}:role:{member.Role}",
                cancellationToken);
        }

        await _store.SaveChangesAsync(cancellationToken);

        return ProjectOperationResult<ProjectMemberView>.Success(
            new ProjectMemberView(
                member.UserId,
                member.User.DisplayName.Value,
                member.User.Email.Value,
                member.Role,
                member.AddedAt),
            "Project member role updated");
    }
}
