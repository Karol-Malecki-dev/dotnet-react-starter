using Domain.Entities;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Provides the persistence operations required by ProjectTask commands.
/// </summary>
public interface IProjectTaskCommandStore
{
    Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    void AddTask(ProjectTask task);
    void RemoveTask(ProjectTask task);
    void ReplaceTaskLabels(ProjectTask task, IReadOnlyCollection<ProjectTaskLabel> previousLabels);
    void AddActivity(ProjectActivity activity);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}