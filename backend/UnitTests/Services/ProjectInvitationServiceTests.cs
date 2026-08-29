using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace UnitTests.Services;

public sealed class ProjectInvitationServiceTests
{
    private readonly Mock<IProjectMembershipStore> _membershipStore = new();
    private readonly Mock<IProjectInvitationStore> _invitationStore = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly ApplicationDbContext _dbContext = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public async Task Create_invitation_returns_not_found_when_project_is_not_owned()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService();

        var result = await service.CreateProjectInvitationAsync(new CreateProjectInvitationCommand(
            ownerId, projectId, "member@example.com", ProjectMemberRole.Member));

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _invitationStore.Verify(store => store.GetActiveUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_invitation_returns_validation_error_for_owner_role()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.CreateProjectInvitationAsync(new CreateProjectInvitationCommand(
            ownerId, projectId, "member@example.com", ProjectMemberRole.Owner));

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _invitationStore.Verify(store => store.GetActiveUserByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_invitation_returns_conflict_when_user_has_pending_invitation()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _invitationStore.Setup(store => store.GetActiveUserByEmailAsync("member@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, DisplayName = "Member", Email = "member@example.com" });
        _invitationStore.Setup(store => store.IsMemberAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _invitationStore.Setup(store => store.HasPendingInvitationAsync(projectId, userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.CreateProjectInvitationAsync(new CreateProjectInvitationCommand(
            ownerId, projectId, " member@example.com ", ProjectMemberRole.Viewer));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _invitationStore.Verify(store => store.AddInvitation(It.IsAny<ProjectInvitation>()), Times.Never);
        _invitationStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_invitation_adds_member_and_marks_invitation_as_accepted()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Test project");
        var projectId = project.Id;
        const string token = "valid-token";
        var invitation = CreateInvitation(ownerId, recipientId, project, token);
        _invitationStore.Setup(store => store.GetInvitationWithDetailsAsync(HashToken(token), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _invitationStore.Setup(store => store.IsMemberAsync(projectId, recipientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateInvitationService();

        var result = await service.AcceptProjectInvitationAsync(recipientId, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectInvitationStatus.Accepted, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        Assert.Contains(invitation.Project.Members, member =>
            member.ProjectId == projectId
            && member.UserId == recipientId
            && member.Role == ProjectMemberRole.Viewer);
        _invitationStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decline_invitation_marks_invitation_as_declined_without_adding_member()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Test project");
        var projectId = project.Id;
        const string token = "decline-token";
        var invitation = CreateInvitation(ownerId, recipientId, project, token);
        _invitationStore.Setup(store => store.GetInvitationWithDetailsAsync(HashToken(token), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var service = CreateInvitationService();

        var result = await service.DeclineProjectInvitationAsync(recipientId, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectInvitationStatus.Declined, invitation.Status);
        Assert.NotNull(invitation.RespondedAt);
        _invitationStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_invitation_returns_not_found_for_wrong_recipient_without_changing_state()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Test project");
        var projectId = project.Id;
        const string token = "recipient-token";
        var invitation = CreateInvitation(ownerId, recipientId, project, token);
        _invitationStore.Setup(store => store.GetInvitationWithDetailsAsync(HashToken(token), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var service = CreateInvitationService();

        var result = await service.AcceptProjectInvitationAsync(Guid.NewGuid(), token);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal(ProjectInvitationStatus.Pending, invitation.Status);
        _invitationStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_expired_invitation_marks_it_expired_without_adding_member()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Test project");
        var projectId = project.Id;
        const string token = "expired-token";
        var invitation = CreateInvitation(ownerId, recipientId, project, token);
        invitation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        _invitationStore.Setup(store => store.GetInvitationWithDetailsAsync(HashToken(token), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var service = CreateInvitationService();

        var result = await service.AcceptProjectInvitationAsync(recipientId, token);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal(ProjectInvitationStatus.Expired, invitation.Status);
        _invitationStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private DatabaseProjectService CreateService()
        => new(_dbContext, _membershipStore.Object, _invitationStore.Object, _notificationService.Object);

    private DatabaseProjectInvitationApplicationService CreateInvitationService()
        => new(_membershipStore.Object, _invitationStore.Object, _notificationService.Object);

    private static ProjectInvitation CreateInvitation(Guid ownerId, Guid recipientId, Project project, string token)
        => new()
        {
            ProjectId = project.Id,
            InvitedUserId = recipientId,
            InvitedByUserId = ownerId,
            Role = ProjectMemberRole.Viewer,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Project = project,
            InvitedUser = new User { Id = recipientId, DisplayName = "Recipient", Email = "recipient@example.com" },
            InvitedByUser = new User { Id = ownerId, DisplayName = "Owner", Email = "owner@example.com" }
        };

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
