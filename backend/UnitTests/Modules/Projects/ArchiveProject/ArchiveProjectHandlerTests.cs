using Application.Features.Projects;
using Application.Modules.Projects.ArchiveProject;
using Domain.Entities;
using Infrastructure.Modules.Projects.ArchiveProject;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace UnitTests.Modules.Projects.ArchiveProject;

public sealed class ArchiveProjectHandlerTests
{
    private readonly Mock<IArchiveProjectStore> _store = new();

    [Fact]
    public async Task Handle_returns_not_found_without_persisting_when_project_is_not_owned()
    {
        var command = CreateCommand();
        _store
            .Setup(store => store.GetOwnedProjectAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_success_without_persisting_when_project_is_already_archived()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Archived project");
        project.Archive();
        ConfigureProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Project already archived", result.Message);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_archives_the_owned_project()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Active project");
        ConfigureProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Project archived", result.Message);
        Assert.True(project.IsArchived);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_maps_provider_concurrency_conflict_and_clears_tracker()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Active project");
        ConfigureProject(command, project);
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);
        _store.Verify(store => store.ClearChangeTracker(), Times.Once);
    }

    private ArchiveProjectHandler CreateHandler()
        => new(_store.Object);

    private void ConfigureProject(ArchiveProjectCommand command, Project project)
        => _store
            .Setup(store => store.GetOwnedProjectAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private static ArchiveProjectCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid());
}
