using Application.Features.Projects;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Modules.Projects.ChangeProjectMemberRole;
using Moq;

namespace UnitTests.Modules.Projects.ChangeProjectMemberRole;

public sealed class ChangeProjectMemberRoleHandlerTests
{
    private readonly Mock<IChangeProjectMemberRoleStore> _store = new();

    [Fact]
    public async Task Handle_returns_not_found_when_project_is_not_owned_by_user()
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
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, ProjectMemberRole.Viewer)]
    [InlineData(false, ProjectMemberRole.Owner)]
    public async Task Handle_rejects_changes_to_or_from_the_owner_role(
        bool targetOwner,
        ProjectMemberRole role)
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var memberId = Guid.NewGuid();
        project.AddMember(memberId);
        var command = new ChangeProjectMemberRoleCommand(
            ownerId,
            project.Id,
            targetOwner ? ownerId : memberId,
            role);
        SetupProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_validation_error_for_unknown_role()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var memberId = Guid.NewGuid();
        project.AddMember(memberId);
        var command = new ChangeProjectMemberRoleCommand(
            ownerId,
            project.Id,
            memberId,
            (ProjectMemberRole)999);
        SetupProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_member_does_not_exist()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new ChangeProjectMemberRoleCommand(
            ownerId,
            project.Id,
            Guid.NewGuid(),
            ProjectMemberRole.Viewer);
        SetupProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_changes_role_and_returns_member_view()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var user = CreateUser();
        var member = project.AddMember(user.Id);
        SetUserNavigation(member, user);
        var command = new ChangeProjectMemberRoleCommand(
            ownerId,
            project.Id,
            user.Id,
            ProjectMemberRole.Viewer);
        SetupProject(command, project);
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectMemberRole.Viewer, member.Role);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.Equal("Member", result.Value.DisplayName);
        Assert.Equal("member@example.com", result.Value.Email);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_forwards_cancellation_token_to_store()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var user = CreateUser();
        var member = project.AddMember(user.Id);
        SetUserNavigation(member, user);
        var command = new ChangeProjectMemberRoleCommand(
            ownerId,
            project.Id,
            user.Id,
            ProjectMemberRole.Viewer);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                ownerId,
                project.Id,
                cancellationToken))
            .ReturnsAsync(project);
        _store
            .Setup(store => store.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await CreateHandler().HandleAsync(command, cancellationToken);

        _store.Verify(store => store.GetOwnedProjectWithMembersAsync(
            ownerId,
            project.Id,
            cancellationToken), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(cancellationToken), Times.Once);
    }

    private void SetupProject(ChangeProjectMemberRoleCommand command, Project project)
        => _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private ChangeProjectMemberRoleHandler CreateHandler()
        => new(_store.Object);

    private static ChangeProjectMemberRoleCommand CreateCommand()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProjectMemberRole.Viewer);

    private static User CreateUser()
        => User.Create(
            EmailAddress.Create("member@example.com"),
            DisplayName.Create("Member"));

    private static void SetUserNavigation(ProjectMember member, User user)
        => typeof(ProjectMember)
            .GetProperty(nameof(ProjectMember.User))!
            .SetValue(member, user);
}
