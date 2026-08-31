using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.ListProjectTaskComments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.ListProjectTaskComments;

/// <summary>
/// EF Core implementation of the list-comments persistence port.
/// </summary>
public sealed class EfListProjectTaskCommentsQueryStore : IListProjectTaskCommentsQueryStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListProjectTaskCommentsQueryStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectTaskCommentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var comments = await _dbContext.ProjectTaskComments
            .AsNoTracking()
            .Where(comment => comment.ProjectTaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => new
            {
                comment.Id,
                comment.ProjectTaskId,
                comment.AuthorUserId,
                AuthorDisplayName = comment.AuthorUser.DisplayName,
                comment.Content,
                comment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return comments
            .Select(comment => new ProjectTaskCommentView(
                comment.Id,
                comment.ProjectTaskId,
                comment.AuthorUserId,
                comment.AuthorDisplayName.Value,
                comment.Content,
                comment.CreatedAt))
            .ToList();
    }
}
