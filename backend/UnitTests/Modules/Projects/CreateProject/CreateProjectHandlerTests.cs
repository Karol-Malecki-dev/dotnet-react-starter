using Application.Features.Projects;
using Application.Modules.Projects.CreateProject;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.Projects.CreateProject;
using Moq;

namespace UnitTests.Modules.Projects.CreateProject;

public sealed class CreateProjectHandlerTests
{
    private readonly Mock<ICreateProjectStore> _store = new();

    [Fact]
    public async Task Handle_creates_project_and_activity_for_the_owner()
    {
        var command = CreateCommand();

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.Equal("Project created", result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(command.OwnerId, result.Value!.OwnerId);
        Assert.Equal(command.Name, result.Value.Name);
        Assert.Equal(command.Description, result.Value.Description);
        Assert.Equal(ProjectMemberRole.Owner, result.Value.CurrentUserRole);
        _store.Verify(store => store.AddProject(It.Is<Project>(
            project => project.OwnerId == command.OwnerId
                && project.Name == command.Name
                && project.Description == command.Description)), Times.Once);
        _store.Verify(store => store.AddActivity(It.Is<ProjectActivity>(
            activity => activity.ProjectId == result.Value.Id
                && activity.ActorUserId == command.OwnerId
                && activity.Type == "project.created"
                && activity.Description.Contains(command.Name, StringComparison.Ordinal))), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_forwards_cancellation_token_to_store()
    {
        var command = CreateCommand();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _store
            .Setup(store => store.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await CreateHandler().HandleAsync(command, cancellationToken);

        _store.Verify(store => store.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task Handle_propagates_persistence_failures()
    {
        var command = CreateCommand();
        var exception = new InvalidOperationException("Persistence failed");
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(command));

        Assert.Same(exception, actualException);
    }

    private CreateProjectHandler CreateHandler()
        => new(_store.Object);

    private static CreateProjectCommand CreateCommand()
        => new(Guid.NewGuid(), "Project name", "Project description");
}
