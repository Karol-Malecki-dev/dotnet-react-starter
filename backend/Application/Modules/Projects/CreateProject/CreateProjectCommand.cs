using Application.Features.Projects;
using MediatR;
using Domain.Entities;

namespace Application.Modules.Projects.CreateProject;

/// <summary>
/// Represents the application input for creating a project.
/// </summary>
public sealed record CreateProjectCommand(
    Guid OwnerId,
    string Name,
    string? Description)
    : IRequest<ProjectOperationResult<ProjectView>>;


/// <summary>
/// Provides the persistence operations required by the create-project slice.
/// </summary>
public interface ICreateProjectStore
{
    void AddProject(Project project);
    void AddActivity(ProjectActivity activity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
