namespace Application.Features.Projects;

/// <summary>
/// Handles project membership use cases.
/// </summary>
public interface IProjectMembershipApplicationService
{
    Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
    Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, Domain.Enums.ProjectMemberRole role);
    Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
}