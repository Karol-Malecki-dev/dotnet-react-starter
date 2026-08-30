using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectManagementService : IProjectManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectMembershipStore _membershipStore;

    public DatabaseProjectManagementService(ApplicationDbContext dbContext, IProjectMembershipStore membershipStore)
    {
        _dbContext = dbContext;
        _membershipStore = membershipStore;
    }

    public async Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.HasProjectAccessAsync(userId, projectId, cancellationToken))
        {
            return ProjectOperationResult<PagedProjectActivityView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = _dbContext.ProjectActivities.AsNoTracking().Where(activity => activity.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(activity => activity.CreatedAt)
            .Skip((safePageNumber - 1) * safePageSize).Take(safePageSize)
            .Select(activity => new
            {
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                ActorDisplayName = activity.ActorUser.DisplayName,
                activity.ProjectTaskId,
                activity.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var activityViews = items
            .Select(activity => new ProjectActivityView(
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                activity.ActorDisplayName.Value,
                activity.ProjectTaskId,
                activity.CreatedAt))
            .ToList();
        return ProjectOperationResult<PagedProjectActivityView>.Success(new PagedProjectActivityView(activityViews, safePageNumber, safePageSize, totalCount));
    }

    public async Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!await _membershipStore.HasProjectAccessAsync(userId, projectId, cancellationToken))
        {
            return ProjectOperationResult<ProjectDashboardView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var today = DateTime.UtcNow.Date;
        var upcomingDeadline = today.AddDays(7);
        var dayAfterUpcomingDeadline = upcomingDeadline.AddDays(1);
        var taskQuery = _dbContext.ProjectTasks.AsNoTracking()
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
            .Take(10)
            .Include(task => task.Labels)
            .ToListAsync(cancellationToken);

        var upcomingTasks = await taskQuery
            .Where(task => task.DueDate.HasValue
                && task.DueDate.Value >= today
                && task.DueDate.Value < dayAfterUpcomingDeadline
                && task.Status != ProjectTaskStatus.Done)
            .OrderBy(task => task.DueDate)
            .Take(10)
            .Include(task => task.Labels)
            .ToListAsync(cancellationToken);

        var recentActivities = await _dbContext.ProjectActivities.AsNoTracking()
            .Where(activity => activity.ProjectId == projectId)
            .OrderByDescending(activity => activity.CreatedAt)
            .Take(5)
            .Select(activity => new
            {
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                ActorDisplayName = activity.ActorUser.DisplayName,
                activity.ProjectTaskId,
                activity.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var recentActivityViews = recentActivities
            .Select(activity => new ProjectActivityView(
                activity.Id,
                activity.Type,
                activity.Description,
                activity.ActorUserId,
                activity.ActorDisplayName.Value,
                activity.ProjectTaskId,
                activity.CreatedAt))
            .ToList();

        return ProjectOperationResult<ProjectDashboardView>.Success(new ProjectDashboardView(
            taskStats?.Total ?? 0,
            taskStats?.Todo ?? 0,
            taskStats?.InProgress ?? 0,
            taskStats?.Done ?? 0,
            taskStats?.LowPriority ?? 0,
            taskStats?.NormalPriority ?? 0,
            taskStats?.HighPriority ?? 0,
            overdueTasks.Select(MapDashboardTask).ToList(),
            upcomingTasks.Select(MapDashboardTask).ToList(),
            recentActivityViews));
    }

    private void AddActivity(Guid projectId, Guid actorUserId, string type, string description)
    {
        _membershipStore.AddActivity(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = actorUserId,
            Type = type,
            Description = description
        });
    }

    private static ProjectTaskView MapDashboardTask(ProjectTask task) => new(
        task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority,
        task.DueDate, task.AssignedUserId, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt, task.ConcurrencyStamp,
        task.Labels.OrderBy(label => label.Name).Select(label => label.Name).ToList());

}
