using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// Coordinates project-member role changes through the project aggregate.
/// </summary>
public sealed class ChangeProjectMemberRoleHandler : IRequestHandler<ChangeProjectMemberRoleCommand, ProjectOperationResult<ProjectMemberView>>
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

    public async Task<ProjectOperationResult<ProjectMemberView>> Handle(
        ChangeProjectMemberRoleCommand request,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectWithMembersAsync(
            request.OwnerId,
            request.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (request.UserId == request.OwnerId || request.Role == ProjectMemberRole.Owner)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "The project owner role cannot be changed");
        }

        if (request.Role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Invalid project member role");
        }

        if (!project.Members.Any(member => member.UserId == request.UserId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project member not found");
        }

        ProjectMember member;
        try
        {
            member = project.ChangeMemberRole(request.UserId, request.Role);
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
                request.UserId,
                NotificationType.ProjectMemberRoleChanged,
                "Project role changed",
                $"Your role in '{project.Name}' changed to {member.Role}.",
                "project",
                project.Id,
                project.Id,
                $"project:{project.Id}:member:{request.UserId}:role:{member.Role}",
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
