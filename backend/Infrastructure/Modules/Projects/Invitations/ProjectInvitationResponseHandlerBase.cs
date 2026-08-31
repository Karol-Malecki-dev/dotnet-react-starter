using Application.Features.Projects;
using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.Invitations;

/// <summary>
/// Centralizes security, concurrency, and atomic persistence rules shared by invitation responses.
/// </summary>
public abstract class ProjectInvitationResponseHandlerBase
{
    private readonly IProjectInvitationResponseStore _store;
    private readonly IProjectInvitationNotificationWriter _notificationWriter;

    protected ProjectInvitationResponseHandlerBase(
        IProjectInvitationResponseStore store,
        IProjectInvitationNotificationWriter notificationWriter)
    {
        _store = store;
        _notificationWriter = notificationWriter;
    }

    protected async Task<ProjectOperationResult<ProjectInvitationView>> HandleResponseAsync(
        Guid userId,
        string token,
        ProjectInvitationStatus responseStatus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Invitation token is required");
        }

        var invitation = await _store.GetByTokenHashAsync(
            ProjectInvitationToken.Hash(token),
            cancellationToken);
        if (invitation is null || invitation.InvitedUserId != userId)
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project invitation not found");
        }

        if (invitation.Status != ProjectInvitationStatus.Pending)
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "Project invitation has already been answered");
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow || invitation.Project.IsArchived)
        {
            invitation.Status = ProjectInvitationStatus.Expired;
            invitation.ConcurrencyStamp = Guid.NewGuid().ToString("N");

            try
            {
                await _store.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ConcurrentResponse();
            }

            return ProjectOperationResult<ProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "Project invitation has expired");
        }

        if (responseStatus == ProjectInvitationStatus.Accepted)
        {
            if (await _store.IsMemberAsync(
                    invitation.ProjectId,
                    userId,
                    cancellationToken))
            {
                return ProjectOperationResult<ProjectInvitationView>.Failure(
                    ProjectOperationStatus.Conflict,
                    "User is already a project member");
            }

            try
            {
                var member = invitation.Project.AddMember(userId, invitation.Role);
                _store.AddMember(member);
            }
            catch (InvalidOperationException)
            {
                return ProjectOperationResult<ProjectInvitationView>.Failure(
                    ProjectOperationStatus.Conflict,
                    "User is already a project member");
            }
        }

        invitation.Status = responseStatus;
        invitation.RespondedAt = DateTime.UtcNow;
        invitation.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = invitation.ProjectId,
            ActorUserId = userId,
            Type = responseStatus == ProjectInvitationStatus.Accepted
                ? "invitation.accepted"
                : "invitation.declined",
            Description = responseStatus == ProjectInvitationStatus.Accepted
                ? "accepted a project invitation."
                : "declined a project invitation."
        });
        await _notificationWriter.AddInvitationResponseNotificationAsync(
            invitation.InvitedByUserId,
            invitation.ProjectId,
            invitation.Id,
            invitation.Project.Name,
            invitation.InvitedUser.DisplayName.Value,
            responseStatus,
            cancellationToken);

        try
        {
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrentResponse();
        }
        catch (DbUpdateException exception) when (
            PostgreSqlErrorClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_ProjectMembers_ProjectId_UserId"))
        {
            return ProjectOperationResult<ProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "User is already a project member");
        }

        return ProjectOperationResult<ProjectInvitationView>.Success(
            ProjectInvitationViewMapper.Map(invitation),
            "Project invitation updated");
    }

    private static ProjectOperationResult<ProjectInvitationView> ConcurrentResponse()
        => ProjectOperationResult<ProjectInvitationView>.Failure(
            ProjectOperationStatus.Conflict,
            "Project invitation was answered concurrently; refresh and retry");
}
