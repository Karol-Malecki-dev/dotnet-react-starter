namespace Application.Features.Projects;

/// <summary>
/// Handles project membership use cases.
/// </summary>
public interface IProjectMembershipApplicationService
{
    Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, Domain.Enums.ProjectMemberRole role, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}