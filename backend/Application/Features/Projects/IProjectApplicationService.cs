namespace Application.Features.Projects;

public interface IProjectApplicationService
{
    Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid ownerId, bool includeArchived = false, string scope = "all");
    Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid ownerId, Guid projectId, bool includeArchived = false);
    Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command);
    Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command);
    Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
    Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, Domain.Enums.ProjectMemberRole role);
    Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
    Task<ProjectOperationResult<CreatedProjectInvitationView>> CreateProjectInvitationAsync(CreateProjectInvitationCommand command);
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetProjectInvitationsAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetMyProjectInvitationsAsync(Guid userId);
    Task<ProjectOperationResult<ProjectInvitationView>> AcceptProjectInvitationAsync(Guid userId, string token);
    Task<ProjectOperationResult<ProjectInvitationView>> DeclineProjectInvitationAsync(Guid userId, string token);
    Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize);
    Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId);
}
