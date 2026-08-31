using Application.Features.ProjectManagement.Tasks;

namespace Application.Modules.ProjectTasks.Dashboard;

/// <summary>
/// Immutable task metrics and due-date lists exposed to the Projects dashboard.
/// </summary>
public sealed record ProjectTaskDashboardSnapshot(
    int TotalTasks,
    int TodoTasks,
    int InProgressTasks,
    int DoneTasks,
    int LowPriorityTasks,
    int NormalPriorityTasks,
    int HighPriorityTasks,
    IReadOnlyList<ProjectTaskView> OverdueTasks,
    IReadOnlyList<ProjectTaskView> UpcomingTasks);

/// <summary>
/// Public read port owned by ProjectTasks for project-level dashboard composition.
/// </summary>
public interface IProjectTaskDashboardReader
{
    Task<ProjectTaskDashboardSnapshot> ReadAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
