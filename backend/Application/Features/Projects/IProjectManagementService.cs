namespace Application.Features.Projects;

/// <summary>
/// Handles project lifecycle and project-level read use cases.
/// </summary>
public interface IProjectManagementService
{
    Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid userId, bool includeArchived = false, string scope = "all");
    Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid userId, Guid projectId, bool includeArchived = false);
    Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command);
    Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command);
    Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize);
    Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId);
}