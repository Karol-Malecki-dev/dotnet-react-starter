using Application.Modules.ProjectTasks.DeleteProjectTaskComment;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.DeleteProjectTaskComment;

/// <summary>
/// EF Core implementation of the delete-comment persistence port.
/// </summary>
public sealed class EfDeleteProjectTaskCommentStore : IDeleteProjectTaskCommentStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfDeleteProjectTaskCommentStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectTaskComment?> GetAsync(
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectTaskComments
            .FirstOrDefaultAsync(
                comment => comment.Id == commentId && comment.ProjectTaskId == taskId,
                cancellationToken);

    public void Remove(ProjectTaskComment comment)
    {
        _dbContext.ProjectTaskComments.Remove(comment);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
