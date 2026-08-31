using Application.Features.Projects;
using Application.Modules.Projects.AddProjectMember;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Modules.Projects.AddProjectMember;
using Moq;

namespace UnitTests.Modules.Projects.AddProjectMember;

public sealed class AddProjectMemberHandlerTests
{
    private readonly Mock<IAddProjectMemberStore> _store = new();
    private readonly Mock<IAddProjectMemberNotificationWriter> _notificationWriter = new();

    [Fact]
    public async Task Handle_returns_not_found_without_querying_user_when_project_is_not_owned_by_user()
    {
        var command = CreateCommand();
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(store => store.GetActiveUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_user_is_missing_or_inactive()
    {
        var command = CreateCommand();
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(command.OwnerId, "Project"));
        _store
            .Setup(store => store.GetActiveUserAsync(
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(store => store.IsMemberAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_conflict_when_user_is_already_a_member()
    {
        var command = CreateCommand();
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(command.OwnerId, "Project"));
        _store
            .Setup(store => store.GetActiveUserAsync(
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(command.UserId));
        _store
            .Setup(store => store.IsMemberAsync(
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _store.Verify(store => store.AddMember(It.IsAny<ProjectMember>()), Times.Never);
        _notificationWriter.Verify(writer => writer.AddProjectMemberNotificationAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_adds_member_activity_and_notification_in_one_save()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new AddProjectMemberCommand(ownerId, project.Id, Guid.NewGuid());
        var user = CreateUser(command.UserId);
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _store
            .Setup(store => store.GetActiveUserAsync(
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _store
            .Setup(store => store.IsMemberAsync(
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.Equal(command.UserId, result.Value!.UserId);
        Assert.Equal(ProjectMemberRole.Member, result.Value.Role);
        _store.Verify(store => store.AddMember(It.Is<ProjectMember>(member =>
            member.ProjectId == command.ProjectId
            && member.UserId == command.UserId
            && member.Role == ProjectMemberRole.Member)), Times.Once);
        _store.Verify(store => store.AddActivity(It.Is<ProjectActivity>(activity =>
            activity.ProjectId == command.ProjectId
            && activity.ActorUserId == command.OwnerId
            && activity.Type == "member.added"
            && activity.Description == "added Member to the project.")), Times.Once);
        _notificationWriter.Verify(writer => writer.AddProjectMemberNotificationAsync(
            command.UserId,
            command.ProjectId,
            "Project",
            It.IsAny<CancellationToken>()), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_save_when_notification_staging_fails()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new AddProjectMemberCommand(ownerId, project.Id, Guid.NewGuid());
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        _store
            .Setup(store => store.GetActiveUserAsync(
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUser(command.UserId));
        _store
            .Setup(store => store.IsMemberAsync(
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _notificationWriter
            .Setup(writer => writer.AddProjectMemberNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification persistence failed."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(command));

        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_forwards_cancellation_token_to_all_operations()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new AddProjectMemberCommand(ownerId, project.Id, Guid.NewGuid());
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                cancellationToken))
            .ReturnsAsync(project);
        _store
            .Setup(store => store.GetActiveUserAsync(command.UserId, cancellationToken))
            .ReturnsAsync(CreateUser(command.UserId));
        _store
            .Setup(store => store.IsMemberAsync(command.ProjectId, command.UserId, cancellationToken))
            .ReturnsAsync(false);
        _store
            .Setup(store => store.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await CreateHandler().HandleAsync(command, cancellationToken);

        _store.Verify(store => store.GetOwnedProjectWithMembersAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken), Times.Once);
        _store.Verify(store => store.GetActiveUserAsync(command.UserId, cancellationToken), Times.Once);
        _store.Verify(store => store.IsMemberAsync(command.ProjectId, command.UserId, cancellationToken), Times.Once);
        _notificationWriter.Verify(writer => writer.AddProjectMemberNotificationAsync(
            command.UserId,
            command.ProjectId,
            "Project",
            cancellationToken), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(cancellationToken), Times.Once);
    }

    private AddProjectMemberHandler CreateHandler()
        => new(_store.Object, _notificationWriter.Object);

    private static AddProjectMemberCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    private static User CreateUser(Guid userId)
        => User.Create(
            EmailAddress.Create("member@example.com"),
            DisplayName.Create("Member"),
            id: userId);
}
