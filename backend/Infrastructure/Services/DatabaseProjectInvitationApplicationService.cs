using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

public sealed class DatabaseProjectInvitationApplicationService : IProjectInvitationApplicationService
{
    private readonly IProjectMembershipStore _membershipStore;
    private readonly IProjectInvitationStore _invitationStore;
    private readonly INotificationService _notificationService;

    public DatabaseProjectInvitationApplicationService(
        IProjectMembershipStore membershipStore,
        IProjectInvitationStore invitationStore,
        INotificationService notificationService)
    {
        _membershipStore = membershipStore;
        _invitationStore = invitationStore;
        _notificationService = notificationService;
    }

    public async Task<ProjectOperationResult<CreatedProjectInvitationView>> CreateProjectInvitationAsync(CreateProjectInvitationCommand command, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(command.OwnerId, command.ProjectId, cancellationToken))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (command.Role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(ProjectOperationStatus.ValidationError, "Invitation role must be Member or Viewer");
        }

        var email = command.Email.Trim();
        var invitedUser = await _invitationStore.GetActiveUserByEmailAsync(email, cancellationToken);
        if (invitedUser is null)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(ProjectOperationStatus.NotFound, "An active user with this email was not found");
        }

        if (invitedUser.Id == command.OwnerId || await _invitationStore.IsMemberAsync(command.ProjectId, invitedUser.Id, cancellationToken))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
        }

        if (await _invitationStore.HasPendingInvitationAsync(command.ProjectId, invitedUser.Id, DateTime.UtcNow, cancellationToken))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(ProjectOperationStatus.Conflict, "User already has a pending invitation");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new ProjectInvitation
        {
            ProjectId = command.ProjectId,
            InvitedUserId = invitedUser.Id,
            InvitedByUserId = command.OwnerId,
            Role = command.Role,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _invitationStore.AddInvitation(invitation);
        AddActivity(command.ProjectId, command.OwnerId, "invitation.created", $"invited {invitedUser.DisplayName} to the project.");
        await _invitationStore.SaveChangesAsync(cancellationToken);

        var projectName = await _invitationStore.GetProjectNameAsync(command.ProjectId, cancellationToken);
        var inviterName = await _invitationStore.GetUserDisplayNameAsync(command.OwnerId, cancellationToken);
        await _notificationService.CreateAsync(invitedUser.Id, NotificationType.ProjectInvitation,
            "Project invitation", $"{inviterName} invited you to join '{projectName}'.", "ProjectInvitation", invitation.Id,
            cancellationToken: cancellationToken);

        return ProjectOperationResult<CreatedProjectInvitationView>.Success(
            new CreatedProjectInvitationView(MapInvitation(invitation, projectName, invitedUser, inviterName), token),
            "Project invitation created",
            201);
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetProjectInvitationsAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.OwnedProjectExistsAsync(ownerId, projectId, cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var invitations = await _invitationStore.GetProjectInvitationsAsync(projectId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations.Select(MapInvitation).ToList());
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetMyProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var invitations = await _invitationStore.GetUserPendingInvitationsAsync(userId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations.Select(MapInvitation).ToList());
    }

    public Task<ProjectOperationResult<ProjectInvitationView>> AcceptProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default)
        => RespondToProjectInvitationAsync(userId, token, ProjectInvitationStatus.Accepted, cancellationToken);

    public Task<ProjectOperationResult<ProjectInvitationView>> DeclineProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default)
        => RespondToProjectInvitationAsync(userId, token, ProjectInvitationStatus.Declined, cancellationToken);

    private async Task<ProjectOperationResult<ProjectInvitationView>> RespondToProjectInvitationAsync(Guid userId, string token, ProjectInvitationStatus responseStatus, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(ProjectOperationStatus.ValidationError, "Invitation token is required");
        }

        var invitation = await _invitationStore.GetInvitationWithDetailsAsync(HashToken(token), cancellationToken);
        if (invitation is null || invitation.InvitedUserId != userId)
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(ProjectOperationStatus.NotFound, "Project invitation not found");
        }

        if (invitation.Status != ProjectInvitationStatus.Pending)
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(ProjectOperationStatus.Conflict, "Project invitation has already been answered");
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow || invitation.Project.IsArchived)
        {
            invitation.Status = ProjectInvitationStatus.Expired;
            await _invitationStore.SaveChangesAsync(cancellationToken);
            return ProjectOperationResult<ProjectInvitationView>.Failure(ProjectOperationStatus.Conflict, "Project invitation has expired");
        }

        if (responseStatus == ProjectInvitationStatus.Accepted)
        {
            if (await _invitationStore.IsMemberAsync(invitation.ProjectId, userId, cancellationToken))
            {
                return ProjectOperationResult<ProjectInvitationView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
            }

            _invitationStore.AddMember(new ProjectMember
            {
                ProjectId = invitation.ProjectId,
                UserId = userId,
                Role = invitation.Role
            });
        }

        invitation.Status = responseStatus;
        invitation.RespondedAt = DateTime.UtcNow;
        AddActivity(invitation.ProjectId, userId,
            responseStatus == ProjectInvitationStatus.Accepted ? "invitation.accepted" : "invitation.declined",
            responseStatus == ProjectInvitationStatus.Accepted ? "accepted a project invitation." : "declined a project invitation.");
        await _invitationStore.SaveChangesAsync(cancellationToken);

        await _notificationService.CreateAsync(invitation.InvitedByUserId, NotificationType.ProjectInvitation,
            "Project invitation response", $"{invitation.InvitedUser.DisplayName} {responseStatus.ToString().ToLowerInvariant()} the invitation to '{invitation.Project.Name}'.",
            "ProjectInvitation", invitation.Id, cancellationToken: cancellationToken);

        return ProjectOperationResult<ProjectInvitationView>.Success(MapInvitation(invitation), "Project invitation updated");
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

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static ProjectInvitationView MapInvitation(ProjectInvitation invitation)
        => MapInvitation(invitation, invitation.Project.Name, invitation.InvitedUser, invitation.InvitedByUser.DisplayName);

    private static ProjectInvitationView MapInvitation(ProjectInvitation invitation, string projectName, User invitedUser, string invitedByDisplayName)
        => new(
            invitation.Id,
            invitation.ProjectId,
            projectName,
            invitation.InvitedUserId,
            invitedUser.DisplayName,
            invitedUser.Email,
            invitedByDisplayName,
            invitation.Role,
            invitation.Status == ProjectInvitationStatus.Pending && invitation.ExpiresAt <= DateTime.UtcNow ? ProjectInvitationStatus.Expired : invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt);
}
