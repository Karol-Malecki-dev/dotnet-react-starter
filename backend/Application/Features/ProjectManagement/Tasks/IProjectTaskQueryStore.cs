using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Provides the persistence query required to list project tasks.
/// </summary>
public interface IProjectTaskQueryStore
{
    Task<PagedProjectTaskView> QueryAsync(ProjectTaskQuery query, CancellationToken cancellationToken = default);
}