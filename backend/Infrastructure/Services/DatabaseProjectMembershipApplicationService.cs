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

    public async Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.HasProjectAccessAsync(ownerId, projectId, cancellationToken))
        {
            return ProjectOperationResult<List<ProjectMemberView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var members = await _membershipStore.GetMembersAsync(projectId, cancellationToken);
        return ProjectOperationResult<List<ProjectMemberView>>.Success(members.ToList());
    }

    public async Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId, cancellationToken))
        {
            return ProjectOperationResult<List<ProjectMemberUserView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var users = await _membershipStore.GetAvailableUsersAsync(projectId, cancellationToken);
        return ProjectOperationResult<List<ProjectMemberUserView>>.Success(users.ToList());
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var project = await _membershipStore.GetOwnedProjectWithMembersAsync(ownerId, projectId, cancellationToken);
        if (project is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var user = await _membershipStore.GetActiveUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "User not found or inactive");
        }

        if (await _membershipStore.IsMemberAsync(projectId, userId, cancellationToken))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
        }

        ProjectMember member;
        try
        {
            member = project.AddMember(userId);
        }
        catch (InvalidOperationException)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
        }

        _membershipStore.AddMember(member);
        AddActivity(projectId, ownerId, "member.added", $"added {user.DisplayName} to the project.");
        await _membershipStore.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(
            user.Id,
            NotificationType.ProjectInvitation,
            "You joined a project",
            $"You were added to the project '{project.Name}'.",
            "Project",
            projectId,
            cancellationToken: cancellationToken);

        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            user.Id,
            user.DisplayName,
            user.Email,
            member.Role,
            member.AddedAt), "Project member added", 201);
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, ProjectMemberRole role, CancellationToken cancellationToken = default)
    {
        var project = await _membershipStore.GetOwnedProjectWithMembersAsync(ownerId, projectId, cancellationToken);
        if (project is null)
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

        if (!project.Members.Any(member => member.UserId == userId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        ProjectMember member;
        try
        {
            member = project.ChangeMemberRole(userId, role);
        }
        catch (InvalidOperationException)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "The project member role cannot be changed");
        }

        await _membershipStore.SaveChangesAsync(cancellationToken);
        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            member.UserId,
            member.User.DisplayName,
            member.User.Email,
            member.Role,
            member.AddedAt), "Project member role updated");
    }

    public async Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var project = await _membershipStore.GetOwnedProjectWithMembersAsync(ownerId, projectId, cancellationToken);
        if (project is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (userId == ownerId)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Conflict, "Project owner cannot be removed");
        }

        var member = project.Members.FirstOrDefault(candidate => candidate.UserId == userId);
        if (member is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        var assignedTasks = await _membershipStore.GetAssignedTasksAsync(projectId, userId, cancellationToken);
        foreach (var task in assignedTasks)
        {
            task.Unassign();
        }

        project.RemoveMember(userId);
        _membershipStore.RemoveMember(member);
        AddActivity(projectId, ownerId, "member.removed", "removed a project member.");
        await _membershipStore.SaveChangesAsync(cancellationToken);

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
