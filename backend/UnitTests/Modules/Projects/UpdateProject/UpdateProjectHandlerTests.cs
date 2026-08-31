using Application.Features.Projects;
using Application.Modules.Projects.UpdateProject;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.Projects.UpdateProject;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace UnitTests.Modules.Projects.UpdateProject;

public sealed class UpdateProjectHandlerTests
{
    private readonly Mock<IUpdateProjectStore> _store = new();

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
    public async Task Handle_rejects_updates_to_archived_projects()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Archived project");
        project.Archive();
        ConfigureProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("Archived project cannot be updated", result.Message);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_a_stale_concurrency_stamp_without_mutating_the_project()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Original project", "Original description");
        ConfigureProject(command, project);

        var result = await CreateHandler().HandleAsync(
            command with
            {
                ExpectedConcurrencyStamp = "stale-stamp"
            });

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("Original project", project.Name);
        Assert.Equal("Original description", project.Description);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_updates_the_owned_project()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Original project", "Original description");
        ConfigureProject(command, project);

        var result = await CreateHandler().HandleAsync(
            command with
            {
                ExpectedConcurrencyStamp = project.ConcurrencyStamp
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("Project updated", result.Message);
        Assert.Equal("Updated project", project.Name);
        Assert.Equal("Updated description", project.Description);
        Assert.NotNull(result.Value);
        Assert.Equal(ProjectMemberRole.Owner, result.Value!.CurrentUserRole);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_maps_provider_concurrency_conflict_and_clears_tracker()
    {
        var command = CreateCommand();
        var project = Project.Create(command.OwnerId, "Original project");
        ConfigureProject(command, project);
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);
        _store.Verify(store => store.ClearChangeTracker(), Times.Once);
    }

    private UpdateProjectHandler CreateHandler()
        => new(_store.Object);

    private void ConfigureProject(UpdateProjectCommand command, Project project)
        => _store
            .Setup(store => store.GetOwnedProjectAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private static UpdateProjectCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid(), "Updated project", "Updated description");
}
