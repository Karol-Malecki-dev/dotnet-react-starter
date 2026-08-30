namespace Application.Features.Projects;

/// <summary>
/// Handles project lifecycle and project-level read use cases.
/// </summary>
public interface IProjectManagementService
{
    Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
}