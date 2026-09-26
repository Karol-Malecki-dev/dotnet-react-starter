using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Dashboard;
using Application.Modules.Projects.GetProjectDashboard;

namespace Infrastructure.Modules.Projects.GetProjectDashboard;

/// <summary>
/// Composes project-owned activity with a task snapshot obtained through the ProjectTasks port.
/// </summary>
public sealed class GetProjectDashboardHandler : IRequestHandler<GetProjectDashboardQuery, ProjectOperationResult<ProjectDashboardView>>
{
    private const int RecentActivityLimit = 5;
    private readonly IGetProjectDashboardStore _store;
    private readonly IProjectTaskDashboardReader _taskDashboardReader;

    public GetProjectDashboardHandler(
        IGetProjectDashboardStore store,
        IProjectTaskDashboardReader taskDashboardReader)
    {
        _store = store;
        _taskDashboardReader = taskDashboardReader;
    }

    public async Task<ProjectOperationResult<ProjectDashboardView>> Handle(
        GetProjectDashboardQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.HasProjectAccessAsync(
                request.UserId,
                request.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<ProjectDashboardView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var taskSnapshot = await _taskDashboardReader.ReadAsync(
            request.ProjectId,
            cancellationToken);
        var recentActivity = await _store.GetRecentActivityAsync(
            request.ProjectId,
            RecentActivityLimit,
            cancellationToken);

        return ProjectOperationResult<ProjectDashboardView>.Success(
            new ProjectDashboardView(
                taskSnapshot.TotalTasks,
                taskSnapshot.TodoTasks,
                taskSnapshot.InProgressTasks,
                taskSnapshot.DoneTasks,
                taskSnapshot.LowPriorityTasks,
                taskSnapshot.NormalPriorityTasks,
                taskSnapshot.HighPriorityTasks,
                taskSnapshot.OverdueTasks,
                taskSnapshot.UpcomingTasks,
                recentActivity));
    }
}
