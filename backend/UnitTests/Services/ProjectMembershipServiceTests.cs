using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace UnitTests.Services;

public sealed class ProjectMembershipServiceTests
{
    private readonly Mock<IProjectMembershipStore> _membershipStore = new();
    private readonly Mock<IProjectInvitationStore> _invitationStore = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly ApplicationDbContext _dbContext = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public async Task Add_member_returns_conflict_when_user_is_already_a_member()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _membershipStore.Setup(store => store.GetActiveUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, DisplayName = "Member", Email = "member@example.com" });
        _membershipStore.Setup(store => store.IsMemberAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.AddProjectMemberAsync(ownerId, projectId, userId);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _membershipStore.Verify(store => store.AddMember(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task Update_member_role_rejects_owner_role_changes()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.UpdateProjectMemberRoleAsync(ownerId, projectId, ownerId, ProjectMemberRole.Viewer);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _membershipStore.Verify(store => store.GetMemberWithUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Remove_member_rejects_removing_the_project_owner()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _membershipStore.Setup(store => store.OwnedProjectExistsAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var result = await service.RemoveProjectMemberAsync(ownerId, projectId, ownerId);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _membershipStore.Verify(store => store.RemoveMember(It.IsAny<ProjectMember>()), Times.Never);
    }

    private DatabaseProjectService CreateService()
        => new(_dbContext, _membershipStore.Object, _invitationStore.Object, _notificationService.Object);
}