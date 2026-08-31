using Application.Features.Projects;

namespace Application.Modules.Projects.GetProjectDetails;

/// <summary>
/// Requests the details of a project visible to the current user.
/// </summary>
public sealed record GetProjectDetailsQuery(
    Guid UserId,
    Guid ProjectId,
    bool IncludeArchived = false);

/// <summary>
/// Executes the get-project-details use case.
/// </summary>
public interface IGetProjectDetailsHandler
{
    Task<ProjectOperationResult<ProjectView>> HandleAsync(
        GetProjectDetailsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the persistence projection required by the get-project-details slice.
/// </summary>
public interface IGetProjectDetailsStore
{
    Task<ProjectView?> QueryAsync(
        Guid userId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken = default);
}
