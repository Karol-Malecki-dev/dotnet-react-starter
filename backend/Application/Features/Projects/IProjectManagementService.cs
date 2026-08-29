namespace Application.Features.Projects;

/// <summary>
/// Handles project lifecycle and project-level read use cases.
/// </summary>
public interface IProjectManagementService
{
    Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid userId, bool includeArchived = false, string scope = "all", CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid userId, Guid projectId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
}