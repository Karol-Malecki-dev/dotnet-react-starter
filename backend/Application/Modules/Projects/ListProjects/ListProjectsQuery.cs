using Application.Features.Projects;

namespace Application.Modules.Projects.ListProjects;

/// <summary>
/// Requests the projects visible to the current user.
/// </summary>
public sealed record ListProjectsQuery(
    Guid UserId,
    bool IncludeArchived = false,
    string Scope = "all");

/// <summary>
/// Executes the list-projects use case.
/// </summary>
public interface IListProjectsHandler
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectView>>> HandleAsync(
        ListProjectsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the persistence projection required by the list-projects slice.
/// </summary>
public interface IListProjectsStore
{
    Task<IReadOnlyList<ProjectView>> QueryAsync(
        ListProjectsQuery query,
        CancellationToken cancellationToken = default);
}
