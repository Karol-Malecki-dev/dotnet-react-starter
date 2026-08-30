using Application.Features.Projects;
using Domain.Entities;

namespace Application.Modules.Projects.UpdateProject;

/// <summary>
/// Represents the application input for updating a project.
/// </summary>
public sealed record UpdateProjectCommand(
    Guid OwnerId,
    Guid ProjectId,
    string Name,
    string? Description,
    string? ExpectedConcurrencyStamp = null);

/// <summary>
/// Executes the update-project use case.
/// </summary>
public interface IUpdateProjectHandler
{
    Task<ProjectOperationResult<ProjectView>> HandleAsync(
        UpdateProjectCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the persistence operations required by the update-project slice.
/// </summary>
public interface IUpdateProjectStore
{
    Task<Project?> GetOwnedProjectAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearChangeTracker();
}
