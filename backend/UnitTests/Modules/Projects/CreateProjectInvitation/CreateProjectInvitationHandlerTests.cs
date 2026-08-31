using Application.Features.Projects;
using Application.Modules.Projects.CreateProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Modules.Projects.CreateProjectInvitation;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using CreateInvitationCommand = Application.Modules.Projects.CreateProjectInvitation.CreateProjectInvitationCommand;

namespace UnitTests.Modules.Projects.CreateProjectInvitation;

public sealed class CreateProjectInvitationHandlerTests
{
    private readonly Mock<ICreateProjectInvitationStore> _store = new();
    private readonly Mock<IProjectInvitationNotificationWriter> _notificationWriter = new();

    public CreateProjectInvitationHandlerTests()
    {
        _store.Setup(candidate => candidate.GetPendingInvitationsAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task Returns_not_found_without_querying_recipient_when_project_is_not_owned()
    {
        var command = CreateCommand();
        _store.Setup(candidate => candidate.GetOwnedProjectContextAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectInvitationCreationContext?)null);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(candidate => candidate.GetActiveUserByEmailAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_validation_error_for_owner_role()
    {
        var command = CreateCommand(role: ProjectMemberRole.Owner);
        SetupOwnedProject(command);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _store.Verify(candidate => candidate.GetActiveUserByEmailAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_validation_error_for_invalid_email()
    {
        var command = CreateCommand(email: "invalid");
        SetupOwnedProject(command);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _store.Verify(candidate => candidate.GetActiveUserByEmailAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_not_found_when_active_recipient_does_not_exist()
    {
        var command = CreateCommand();
        SetupOwnedProject(command);
        _store.Setup(candidate => candidate.GetActiveUserByEmailAsync(
                "recipient@example.com",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_conflict_when_recipient_is_already_a_member()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        _store.Setup(candidate => candidate.IsMemberAsync(
                command.ProjectId,
                recipient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _store.Verify(candidate => candidate.GetPendingInvitationsAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_conflict_when_recipient_has_an_active_pending_invitation()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        _store.Setup(candidate => candidate.GetPendingInvitationsAsync(
                command.ProjectId,
                recipient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ProjectInvitation
                {
                    ProjectId = command.ProjectId,
                    InvitedUserId = recipient.Id,
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                }
            ]);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _store.Verify(candidate => candidate.AddInvitation(
            It.IsAny<ProjectInvitation>()), Times.Never);
    }

    [Fact]
    public async Task Success_expires_stale_pending_invitations_before_creating_a_replacement()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        var staleInvitation = new ProjectInvitation
        {
            ProjectId = command.ProjectId,
            InvitedUserId = recipient.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var initialConcurrencyStamp = staleInvitation.ConcurrencyStamp;
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        _store.Setup(candidate => candidate.GetPendingInvitationsAsync(
                command.ProjectId,
                recipient.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([staleInvitation]);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectInvitationStatus.Expired, staleInvitation.Status);
        Assert.NotEqual(initialConcurrencyStamp, staleInvitation.ConcurrencyStamp);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Concurrent_pending_invitation_constraint_returns_conflict()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        var postgresException = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: "IX_ProjectInvitations_ProjectId_InvitedUserId_Status");
        _store.Setup(candidate => candidate.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "Invitation write failed",
                postgresException));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("User already has a pending invitation", result.Message);
    }

    [Fact]
    public async Task Concurrent_expired_invitation_replacement_returns_conflict()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        _store.Setup(candidate => candidate.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException(
                "Invitation state changed concurrently"));
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("User already has a pending invitation", result.Message);
    }

    [Fact]
    public async Task Success_stages_invitation_activity_and_notification_before_one_commit()
    {
        var command = CreateCommand(email: " RECIPIENT@EXAMPLE.COM ");
        var recipient = CreateUser();
        var cancellationToken = new CancellationTokenSource().Token;
        ProjectInvitation? stagedInvitation = null;
        SetupOwnedProject(command);
        SetupRecipient(recipient, "recipient@example.com");
        _store.Setup(candidate => candidate.AddInvitation(It.IsAny<ProjectInvitation>()))
            .Callback<ProjectInvitation>(invitation => stagedInvitation = invitation);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(command, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.NotNull(result.Value);
        Assert.NotNull(stagedInvitation);
        Assert.Equal(
            HashToken(result.Value.Token),
            stagedInvitation.TokenHash);
        Assert.NotEqual(result.Value.Token, stagedInvitation.TokenHash);
        Assert.Equal(ProjectInvitationStatus.Pending, result.Value.Invitation.Status);
        _store.Verify(candidate => candidate.AddActivity(
            It.Is<ProjectActivity>(activity =>
                activity.ProjectId == command.ProjectId
                && activity.ActorUserId == command.OwnerId
                && activity.Type == "invitation.created")), Times.Once);
        _notificationWriter.Verify(writer => writer.AddInvitationCreatedNotificationAsync(
            recipient.Id,
            command.ProjectId,
            stagedInvitation.Id,
            "Project",
            "Owner",
            cancellationToken), Times.Once);
        _store.Verify(candidate => candidate.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Notification_staging_failure_prevents_commit()
    {
        var command = CreateCommand();
        var recipient = CreateUser();
        SetupOwnedProject(command);
        SetupRecipient(recipient);
        _notificationWriter.Setup(writer => writer.AddInvitationCreatedNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification staging failed"));
        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateProjectInvitationHandler CreateHandler()
        => new(_store.Object, _notificationWriter.Object);

    private void SetupOwnedProject(CreateInvitationCommand command)
        => _store.Setup(candidate => candidate.GetOwnedProjectContextAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectInvitationCreationContext("Project", "Owner"));

    private void SetupRecipient(User recipient, string email = "recipient@example.com")
        => _store.Setup(candidate => candidate.GetActiveUserByEmailAsync(
                email,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recipient);

    private static CreateInvitationCommand CreateCommand(
        string email = "recipient@example.com",
        ProjectMemberRole role = ProjectMemberRole.Viewer)
        => new(Guid.NewGuid(), Guid.NewGuid(), email, role);

    private static User CreateUser()
        => User.Create(
            EmailAddress.Create("recipient@example.com"),
            DisplayName.Create("Recipient"),
            id: Guid.NewGuid());

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
