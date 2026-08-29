using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
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
        _membershipStore.Setup(store => store.GetOwnedProjectWithMembersAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(ownerId, "Project"));
        _membershipStore.Setup(store => store.GetActiveUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, DisplayName = "Member", Email = EmailAddress.Create("member@example.com") });
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
        _membershipStore.Setup(store => store.GetOwnedProjectWithMembersAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(ownerId, "Project"));
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
        _membershipStore.Setup(store => store.GetOwnedProjectWithMembersAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(ownerId, "Project"));
        var service = CreateService();

        var result = await service.RemoveProjectMemberAsync(ownerId, projectId, ownerId);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _membershipStore.Verify(store => store.RemoveMember(It.IsAny<ProjectMember>()), Times.Never);
    }

    [Fact]
    public async Task Remove_member_unassigns_tasks_and_commits_the_transaction()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var member = project.AddMember(memberId);
        var task = ProjectTask.Create(
            projectId,
            "Assigned task",
            null,
            ProjectTaskPriority.Normal,
            null,
            memberId,
            ownerId);
        var transaction = new Mock<IProjectTransaction>();
        transaction.Setup(value => value.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _membershipStore.Setup(store => store.GetOwnedProjectWithMembersAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _membershipStore.Setup(store => store.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        _membershipStore.Setup(store => store.GetAssignedTasksAsync(projectId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProjectTask> { task });
        _membershipStore.Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService();

        var result = await service.RemoveProjectMemberAsync(ownerId, projectId, memberId);

        Assert.True(result.IsSuccess);
        Assert.Null(task.AssignedUserId);
        _membershipStore.Verify(store => store.RemoveMember(member), Times.Once);
        _membershipStore.Verify(store => store.AddActivity(It.Is<ProjectActivity>(activity =>
            activity.ProjectId == projectId
            && activity.ActorUserId == ownerId
            && activity.Type == "member.removed")), Times.Once);
        _membershipStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(value => value.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private DatabaseProjectService CreateService()
        => new(_dbContext, _membershipStore.Object, _invitationStore.Object, _notificationService.Object);
}