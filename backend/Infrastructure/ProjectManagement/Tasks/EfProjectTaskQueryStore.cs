using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ProjectManagement.Tasks;

/// <summary>
/// EF Core implementation of the ProjectTask list query.
/// </summary>
public sealed class EfProjectTaskQueryStore : IProjectTaskQueryStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskQueryStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedProjectTaskView> QueryAsync(
        ProjectTaskQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var taskQuery = _dbContext.ProjectTasks
            .AsNoTracking()
            .Include(task => task.Labels)
            .Where(task => task.ProjectId == query.ProjectId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            taskQuery = taskQuery.Where(task => task.Title.Contains(search)
                || (task.Description != null && task.Description.Contains(search))
                || task.Labels.Any(label => label.Name.Contains(search)));
        }
        if (query.Status.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.Status == query.Status.Value);
        }
        if (query.Priority.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.Priority == query.Priority.Value);
        }
        if (query.AssignedUserId.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.AssignedUserId == query.AssignedUserId.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Label))
        {
            var label = query.Label.Trim();
            taskQuery = taskQuery.Where(task => task.Labels.Any(taskLabel => taskLabel.Name == label));
        }
        if (query.DueBefore.HasValue)
        {
            taskQuery = taskQuery.Where(task => task.DueDate.HasValue && task.DueDate <= query.DueBefore.Value);
        }

        var totalCount = await taskQuery.CountAsync(cancellationToken);
        var orderedTaskQuery = (query.SortBy, query.SortDirection) switch
        {
            (ProjectTaskSortBy.CreatedAt, SortDirection.Descending) => taskQuery.OrderByDescending(task => task.CreatedAt),
            (ProjectTaskSortBy.CreatedAt, _) => taskQuery.OrderBy(task => task.CreatedAt),
            (ProjectTaskSortBy.Priority, SortDirection.Descending) => taskQuery.OrderByDescending(task => task.Priority),
            (ProjectTaskSortBy.Priority, _) => taskQuery.OrderBy(task => task.Priority),
            (ProjectTaskSortBy.DueDate, SortDirection.Descending) => taskQuery.OrderByDescending(task => task.DueDate),
            _ => taskQuery.OrderBy(task => task.DueDate)
        };
        var tasks = await orderedTaskQuery
            .ThenBy(task => task.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedProjectTaskView(
            tasks.Select(MapToView).ToList(), pageNumber, pageSize, totalCount);
    }

    private static ProjectTaskView MapToView(Domain.Entities.ProjectTask task) => new(
        task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority,
        task.DueDate, task.AssignedUserId, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt,
        task.Labels.OrderBy(label => label.Name).Select(label => label.Name).ToList());
}