using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.ListProjects;

/// <summary>
/// Requests the projects visible to the current user.
/// </summary>
public sealed record ListProjectsQuery(
    Guid UserId,
    bool IncludeArchived = false,
    string Scope = "all")
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectView>>>;


/// <summary>
/// Provides the persistence projection required by the list-projects slice.
/// </summary>
public interface IListProjectsStore
{
    Task<IReadOnlyList<ProjectView>> QueryAsync(
        ListProjectsQuery query,
        CancellationToken cancellationToken = default);
}
