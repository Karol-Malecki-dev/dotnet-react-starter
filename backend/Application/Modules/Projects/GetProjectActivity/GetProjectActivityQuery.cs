using Application.Features.Projects;

namespace Application.Modules.Projects.GetProjectActivity;

/// <summary>
/// Requests one page of activity visible to a project participant.
/// </summary>
public sealed record GetProjectActivityQuery(
    Guid UserId,
    Guid ProjectId,
    int PageNumber,
    int PageSize);

/// <summary>
/// Executes the get-project-activity use case.
/// </summary>
public interface IGetProjectActivityHandler
{
    Task<ProjectOperationResult<PagedProjectActivityView>> HandleAsync(
        GetProjectActivityQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides access checks and the focused activity projection for one project.
/// </summary>
public interface IGetProjectActivityStore
{
    Task<bool> HasProjectAccessAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<PagedProjectActivityView> QueryAsync(
        Guid projectId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
