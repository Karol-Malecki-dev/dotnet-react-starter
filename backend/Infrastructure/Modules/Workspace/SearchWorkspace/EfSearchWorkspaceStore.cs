using Application.Modules.Workspace.SearchWorkspace;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Workspace.SearchWorkspace;

public sealed class EfSearchWorkspaceStore : ISearchWorkspaceStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfSearchWorkspaceStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkspaceSearchPage> SearchAsync(SearchWorkspaceQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Query.Trim();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 20);

        if (!string.Equals(query.Type, "projectTask", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkspaceSearchPage([], page, pageSize, 0);
        }

        var accessibleProjectIds = _dbContext.Projects
            .Where(project => !project.IsArchived &&
                (project.OwnerId == query.UserId || project.Members.Any(member => member.UserId == query.UserId)))
            .Select(project => project.Id);

        var tasks = _dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => accessibleProjectIds.Contains(task.ProjectId) &&
                (EF.Functions.ILike(task.Title, $"%{normalizedQuery}%") ||
                 (task.Description != null && EF.Functions.ILike(task.Description, $"%{normalizedQuery}%"))));

        var totalCount = await tasks.CountAsync(cancellationToken);
        var items = await tasks
            .OrderBy(task => task.Title)
            .ThenBy(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(task => new WorkspaceSearchResult(
                "projectTask",
                task.Id,
                task.ProjectId,
                task.Title,
                task.Description ?? string.Empty))
            .ToListAsync(cancellationToken);

        return new WorkspaceSearchPage(items, page, pageSize, totalCount);
    }
}
