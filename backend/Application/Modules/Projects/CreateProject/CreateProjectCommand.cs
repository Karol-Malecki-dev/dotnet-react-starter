using Application.Features.Projects;
using Domain.Entities;

namespace Application.Modules.Projects.CreateProject;

/// <summary>
/// Represents the application input for creating a project.
/// </summary>
public sealed record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string? Description);

/// <summary>
/// Executes the create-project use case.
/// </summary>
public interface ICreateProjectHandler
{
    Task<ProjectOperationResult<ProjectView>> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the persistence operations required by the create-project slice.
/// </summary>
public interface ICreateProjectStore
{
    void AddProject(Project project);
    void AddActivity(ProjectActivity activity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
