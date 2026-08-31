using Application.Features.Projects;
using Application.Modules.Projects.CreateProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Modules.Projects.Invitations;
using Microsoft.EntityFrameworkCore;
using CreateInvitationCommand = Application.Modules.Projects.CreateProjectInvitation.CreateProjectInvitationCommand;

namespace Infrastructure.Modules.Projects.CreateProjectInvitation;

/// <summary>
/// Coordinates secure invitation creation and atomic notification staging.
/// </summary>
public sealed class CreateProjectInvitationHandler : ICreateProjectInvitationHandler
{
    private readonly ICreateProjectInvitationStore _store;
    private readonly IProjectInvitationNotificationWriter _notificationWriter;

    public CreateProjectInvitationHandler(
        ICreateProjectInvitationStore store,
        IProjectInvitationNotificationWriter notificationWriter)
    {
        _store = store;
        _notificationWriter = notificationWriter;
    }

    public async Task<ProjectOperationResult<CreatedProjectInvitationView>> HandleAsync(
        CreateInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var context = await _store.GetOwnedProjectContextAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);
        if (context is null)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (command.Role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Invitation role must be Member or Viewer");
        }

        if (!EmailAddress.TryCreate(command.Email, out var emailAddress) || emailAddress is null)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Invitation email has an invalid format");
        }

        var invitedUser = await _store.GetActiveUserByEmailAsync(
            emailAddress.Value,
            cancellationToken);
        if (invitedUser is null)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.NotFound,
                "An active user with this email was not found");
        }

        if (invitedUser.Id == command.OwnerId
            || await _store.IsMemberAsync(
                command.ProjectId,
                invitedUser.Id,
                cancellationToken))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "User is already a project member");
        }

        var now = DateTime.UtcNow;
        var pendingInvitations = await _store.GetPendingInvitationsAsync(
            command.ProjectId,
            invitedUser.Id,
            cancellationToken);
        if (pendingInvitations.Any(invitation => invitation.ExpiresAt > now))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "User already has a pending invitation");
        }

        foreach (var expiredInvitation in pendingInvitations)
        {
            expiredInvitation.Status = ProjectInvitationStatus.Expired;
            expiredInvitation.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }

        var (rawToken, tokenHash) = ProjectInvitationToken.Create();
        var invitation = new ProjectInvitation
        {
            ProjectId = command.ProjectId,
            InvitedUserId = invitedUser.Id,
            InvitedByUserId = command.OwnerId,
            Role = command.Role,
            TokenHash = tokenHash,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };
        _store.AddInvitation(invitation);
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = command.ProjectId,
            ActorUserId = command.OwnerId,
            Type = "invitation.created",
            Description = $"invited {invitedUser.DisplayName.Value} to the project."
        });
        await _notificationWriter.AddInvitationCreatedNotificationAsync(
            invitedUser.Id,
            command.ProjectId,
            invitation.Id,
            context.ProjectName,
            context.InviterDisplayName,
            cancellationToken);

        try
        {
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "User already has a pending invitation");
        }
        catch (DbUpdateException exception) when (
            PostgreSqlErrorClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_ProjectInvitations_ProjectId_InvitedUserId_Status"))
        {
            return ProjectOperationResult<CreatedProjectInvitationView>.Failure(
                ProjectOperationStatus.Conflict,
                "User already has a pending invitation");
        }

        var view = new ProjectInvitationView(
            invitation.Id,
            command.ProjectId,
            context.ProjectName,
            invitedUser.Id,
            invitedUser.DisplayName.Value,
            invitedUser.Email.Value,
            context.InviterDisplayName,
            invitation.Role,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt);

        return ProjectOperationResult<CreatedProjectInvitationView>.Success(
            new CreatedProjectInvitationView(view, rawToken),
            "Project invitation created",
            201);
    }
}
