using Application.Features.ProjectManagement.Tasks;
using Application.Modules.ProjectTasks.Dashboard;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.Dashboard;

/// <summary>
/// EF Core implementation of the dashboard read port owned by ProjectTasks.
/// </summary>
public sealed class EfProjectTaskDashboardReader : IProjectTaskDashboardReader
{
    private const int DueTaskLimit = 10;
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskDashboardReader(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectTaskDashboardSnapshot> ReadAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var dayAfterUpcomingDeadline = today.AddDays(8);
        var taskQuery = _dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);
        var taskStats = await taskQuery
            .GroupBy(_ => 1)
            .Select(tasks => new
            {
                Total = tasks.Count(),
                Todo = tasks.Count(task => task.Status == ProjectTaskStatus.Todo),
                InProgress = tasks.Count(task => task.Status == ProjectTaskStatus.InProgress),
                Done = tasks.Count(task => task.Status == ProjectTaskStatus.Done),
                LowPriority = tasks.Count(task => task.Priority == ProjectTaskPriority.Low),
                NormalPriority = tasks.Count(task => task.Priority == ProjectTaskPriority.Normal),
                HighPriority = tasks.Count(task => task.Priority == ProjectTaskPriority.High)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var overdueTasks = await taskQuery
            .Where(task => task.DueDate.HasValue
                && task.DueDate.Value < today
                && task.Status != ProjectTaskStatus.Done)
            .OrderBy(task => task.DueDate)
            .Take(DueTaskLimit)
            .Include(task => task.Labels)
            .ToListAsync(cancellationToken);
        var upcomingTasks = await taskQuery
            .Where(task => task.DueDate.HasValue
                && task.DueDate.Value >= today
                && task.DueDate.Value < dayAfterUpcomingDeadline
                && task.Status != ProjectTaskStatus.Done)
            .OrderBy(task => task.DueDate)
            .Take(DueTaskLimit)
            .Include(task => task.Labels)
            .ToListAsync(cancellationToken);

        return new ProjectTaskDashboardSnapshot(
            taskStats?.Total ?? 0,
            taskStats?.Todo ?? 0,
            taskStats?.InProgress ?? 0,
            taskStats?.Done ?? 0,
            taskStats?.LowPriority ?? 0,
            taskStats?.NormalPriority ?? 0,
            taskStats?.HighPriority ?? 0,
            overdueTasks.Select(MapTask).ToList(),
            upcomingTasks.Select(MapTask).ToList());
    }

    private static ProjectTaskView MapTask(ProjectTask task)
        => new(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.AssignedUserId,
            task.CreatedByUserId,
            task.CreatedAt,
            task.UpdatedAt,
            task.ConcurrencyStamp,
            task.Labels
                .OrderBy(label => label.Name)
                .Select(label => label.Name)
                .ToList());
}
