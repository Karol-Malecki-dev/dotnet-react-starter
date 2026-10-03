using Application.Modules.ProjectTasks.Shared;

namespace Application.Modules.Projects.Shared;

public sealed record ProjectDashboardView(
    int TotalTasks,
    int TodoTasks,
    int InProgressTasks,
    int DoneTasks,
    int LowPriorityTasks,
    int NormalPriorityTasks,
    int HighPriorityTasks,
    IReadOnlyList<ProjectTaskView> OverdueTasks,
    IReadOnlyList<ProjectTaskView> UpcomingTasks,
    IReadOnlyList<ProjectActivityView> RecentActivities);
