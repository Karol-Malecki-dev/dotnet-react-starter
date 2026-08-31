using Application.Features.Projects;
using Application.Modules.Projects.AcceptProjectInvitation;
using Application.Modules.Projects.DeclineProjectInvitation;
using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Modules.Projects.AcceptProjectInvitation;
using Infrastructure.Modules.Projects.DeclineProjectInvitation;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace UnitTests.Modules.Projects.ProjectInvitationResponses;

public sealed class ProjectInvitationResponseHandlerTests
{
    private readonly Mock<IProjectInvitationResponseStore> _store = new();
    private readonly Mock<IProjectInvitationNotificationWriter> _notificationWriter = new();

    [Fact]
    public async Task Accept_adds_member_and_stages_activity_and_notification_before_one_commit()
    {
        const string token = "accept-token";
        var invitation = CreateInvitation(token);
        var cancellationToken = new CancellationTokenSource().Token;
        SetupInvitation(token, invitation);
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(invitation.InvitedUserId, token),
            cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectInvitationStatus.Accepted, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        _store.Verify(candidate => candidate.AddMember(
            It.Is<ProjectMember>(member =>
                member.ProjectId == invitation.ProjectId
                && member.UserId == invitation.InvitedUserId
                && member.Role == invitation.Role)), Times.Once);
        _store.Verify(candidate => candidate.AddActivity(
            It.Is<ProjectActivity>(activity =>
                activity.Type == "invitation.accepted"
                && activity.ActorUserId == invitation.InvitedUserId)), Times.Once);
        _notificationWriter.Verify(writer => writer.AddInvitationResponseNotificationAsync(
            invitation.InvitedByUserId,
            invitation.ProjectId,
            invitation.Id,
            invitation.Project.Name,
            invitation.InvitedUser.DisplayName.Value,
            ProjectInvitationStatus.Accepted,
            cancellationToken), Times.Once);
        _store.Verify(candidate => candidate.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Decline_does_not_add_member_and_marks_invitation_as_declined()
    {
        const string token = "decline-token";
        var invitation = CreateInvitation(token);
        SetupInvitation(token, invitation);
        var handler = CreateDeclineHandler();

        var result = await handler.HandleAsync(
            new DeclineProjectInvitationCommand(invitation.InvitedUserId, token));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectInvitationStatus.Declined, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        _store.Verify(candidate => candidate.AddMember(
            It.IsAny<ProjectMember>()), Times.Never);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Wrong_recipient_receives_not_found_without_state_change()
    {
        const string token = "private-token";
        var invitation = CreateInvitation(token);
        SetupInvitation(token, invitation);
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(Guid.NewGuid(), token));

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal(ProjectInvitationStatus.Pending, invitation.Status);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Already_answered_invitation_returns_conflict()
    {
        const string token = "answered-token";
        var invitation = CreateInvitation(token);
        invitation.Status = ProjectInvitationStatus.Declined;
        SetupInvitation(token, invitation);
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(invitation.InvitedUserId, token));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _store.Verify(candidate => candidate.IsMemberAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Expired_invitation_is_persisted_as_expired_without_notification()
    {
        const string token = "expired-token";
        var invitation = CreateInvitation(token);
        invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        SetupInvitation(token, invitation);
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(invitation.InvitedUserId, token));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal(ProjectInvitationStatus.Expired, invitation.Status);
        _notificationWriter.Verify(writer => writer.AddInvitationResponseNotificationAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ProjectInvitationStatus>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Existing_member_returns_conflict_without_mutation()
    {
        const string token = "member-token";
        var invitation = CreateInvitation(token);
        SetupInvitation(token, invitation);
        _store.Setup(candidate => candidate.IsMemberAsync(
                invitation.ProjectId,
                invitation.InvitedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(invitation.InvitedUserId, token));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal(ProjectInvitationStatus.Pending, invitation.Status);
        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Notification_staging_failure_prevents_commit()
    {
        const string token = "failure-token";
        var invitation = CreateInvitation(token);
        SetupInvitation(token, invitation);
        _notificationWriter.Setup(writer => writer.AddInvitationResponseNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ProjectInvitationStatus>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification staging failed"));
        var handler = CreateAcceptHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(
            new AcceptProjectInvitationCommand(invitation.InvitedUserId, token)));

        _store.Verify(candidate => candidate.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Provider_concurrency_conflict_returns_conflict_result()
    {
        const string token = "concurrency-token";
        var invitation = CreateInvitation(token);
        SetupInvitation(token, invitation);
        _store.Setup(candidate => candidate.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var handler = CreateDeclineHandler();

        var result = await handler.HandleAsync(
            new DeclineProjectInvitationCommand(invitation.InvitedUserId, token));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Empty_token_returns_validation_error_without_store_access()
    {
        var handler = CreateAcceptHandler();

        var result = await handler.HandleAsync(
            new AcceptProjectInvitationCommand(Guid.NewGuid(), " "));

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _store.Verify(candidate => candidate.GetByTokenHashAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private AcceptProjectInvitationHandler CreateAcceptHandler()
        => new(_store.Object, _notificationWriter.Object);

    private DeclineProjectInvitationHandler CreateDeclineHandler()
        => new(_store.Object, _notificationWriter.Object);

    private void SetupInvitation(string token, ProjectInvitation invitation)
        => _store.Setup(candidate => candidate.GetByTokenHashAsync(
                HashToken(token),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

    private static ProjectInvitation CreateInvitation(string token)
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Test project");
        return new ProjectInvitation
        {
            ProjectId = project.Id,
            InvitedUserId = recipientId,
            InvitedByUserId = ownerId,
            Role = ProjectMemberRole.Viewer,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Project = project,
            InvitedUser = User.Create(
                EmailAddress.Create("recipient@example.com"),
                DisplayName.Create("Recipient"),
                id: recipientId),
            InvitedByUser = User.Create(
                EmailAddress.Create("owner@example.com"),
                DisplayName.Create("Owner"),
                id: ownerId)
        };
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
