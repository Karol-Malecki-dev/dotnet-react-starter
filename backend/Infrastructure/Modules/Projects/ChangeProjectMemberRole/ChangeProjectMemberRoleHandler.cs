using Application.Features.Projects;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// Coordinates project-member role changes through the project aggregate.
/// </summary>
public sealed class ChangeProjectMemberRoleHandler : IChangeProjectMemberRoleHandler
{
    private readonly IChangeProjectMemberRoleStore _store;

    public ChangeProjectMemberRoleHandler(IChangeProjectMemberRoleStore store)
    {
        _store = store;
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
