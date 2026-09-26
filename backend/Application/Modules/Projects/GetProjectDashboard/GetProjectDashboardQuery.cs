using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.GetProjectDashboard;

/// <summary>
/// Requests the dashboard visible to one project participant.
/// </summary>
public sealed record GetProjectDashboardQuery(Guid UserId, Guid ProjectId)
    : IRequest<ProjectOperationResult<ProjectDashboardView>>;


/// <summary>
/// Provides project-owned data needed to compose the dashboard.
/// Task data is intentionally obtained through the ProjectTasks module port.
/// </summary>
public interface IGetProjectDashboardStore
{
    Task<bool> HasProjectAccessAsync(
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectActivityView>> GetRecentActivityAsync(
        Guid projectId,
        int count,
        CancellationToken cancellationToken = default);
}
