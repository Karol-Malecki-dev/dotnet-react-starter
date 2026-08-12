using Domain.Entities;
using Domain.Enums;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Provides the persistence-backed access checks required by ProjectTask use cases.
/// </summary>
public interface IProjectTaskAccess
{
    /// <summary>
    /// Gets the caller's role in an active project, or null when the caller has no access.
    /// </summary>
    Task<ProjectMemberRole?> GetActiveProjectRoleAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a task with its labels when it belongs to the specified project.
    /// </summary>
    Task<ProjectTask?> GetTaskWithLabelsAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
}