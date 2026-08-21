using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectMembershipApplicationService : IProjectMembershipApplicationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectMembershipStore _membershipStore;
    private readonly INotificationService _notificationService;

    public DatabaseProjectMembershipApplicationService(
        ApplicationDbContext dbContext,
        IProjectMembershipStore membershipStore,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _membershipStore = membershipStore;
        _notificationService = notificationService;
    }

    public async Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await _membershipStore.HasProjectAccessAsync(ownerId, projectId))
        {
            return ProjectOperationResult<List<ProjectMemberView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var members = await _membershipStore.GetMembersAsync(projectId);
        return ProjectOperationResult<List<ProjectMemberView>>.Success(members.ToList());
    }

    public async Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<List<ProjectMemberUserView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var users = await _membershipStore.GetAvailableUsersAsync(projectId);
        return ProjectOperationResult<List<ProjectMemberUserView>>.Success(users.ToList());
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var user = await _membershipStore.GetActiveUserAsync(userId);
        if (user is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "User not found or inactive");
        }

        if (await _membershipStore.IsMemberAsync(projectId, userId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
        }

        var member = new ProjectMember { ProjectId = projectId, UserId = userId };
        _membershipStore.AddMember(member);
        AddActivity(projectId, ownerId, "member.added", $"added {user.DisplayName} to the project.");
        await _membershipStore.SaveChangesAsync();

        var projectName = await _dbContext.Projects
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .FirstAsync();
        await _notificationService.CreateAsync(
            user.Id,
            NotificationType.ProjectInvitation,
            "You joined a project",
            $"You were added to the project '{projectName}'.",
            "Project",
            projectId);

        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            user.Id,
            user.DisplayName,
            user.Email,
            member.Role,
            member.AddedAt), "Project member added", 201);
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, ProjectMemberRole role)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (userId == ownerId || role == ProjectMemberRole.Owner)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "The project owner role cannot be changed");
        }

        if (role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.ValidationError, "Invalid project member role");
        }

        var member = await _membershipStore.GetMemberWithUserAsync(projectId, userId);
        if (member is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        member.Role = role;
        await _membershipStore.SaveChangesAsync();
        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            member.UserId,
            member.User.DisplayName,
            member.User.Email,
            member.Role,
            member.AddedAt), "Project member role updated");
    }

    public async Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (userId == ownerId)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Conflict, "Project owner cannot be removed");
        }

        var member = await _membershipStore.GetMemberWithUserAsync(projectId, userId);
        if (member is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        var assignedTasks = await _membershipStore.GetAssignedTasksAsync(projectId, userId);
        foreach (var task in assignedTasks)
        {
            task.Unassign();
        }

        _membershipStore.RemoveMember(member);
        AddActivity(projectId, ownerId, "member.removed", "removed a project member.");
        await _membershipStore.SaveChangesAsync();

        return ProjectOperationResult<bool>.Success(true, "Project member removed");
    }

    private void AddActivity(Guid projectId, Guid actorUserId, string type, string description)
    {
        _membershipStore.AddActivity(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = actorUserId,
            Type = type,
            Description = description
        });
    }
}
