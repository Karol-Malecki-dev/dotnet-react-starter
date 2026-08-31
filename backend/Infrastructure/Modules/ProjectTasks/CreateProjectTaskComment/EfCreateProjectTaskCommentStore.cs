using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskComment;

/// <summary>
/// EF Core implementation of the create-comment persistence port.
/// </summary>
public sealed class EfCreateProjectTaskCommentStore : ICreateProjectTaskCommentStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfCreateProjectTaskCommentStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectTaskCommentView> CreateAsync(
        CreateProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default)
    {
        var comment = new ProjectTaskComment
        {
            ProjectTaskId = command.ProjectTaskId,
            AuthorUserId = command.AuthorUserId,
            Content = command.Content
        };
        _dbContext.ProjectTaskComments.Add(comment);
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = command.ProjectId,
            ActorUserId = command.AuthorUserId,
            ProjectTaskId = command.ProjectTaskId,
            Type = "task.comment-added",
            Description = "added a comment to a task."
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var authorDisplayName = await _dbContext.Users
            .Where(user => user.Id == command.AuthorUserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);

        return new ProjectTaskCommentView(
            comment.Id,
            comment.ProjectTaskId,
            comment.AuthorUserId,
            authorDisplayName.Value,
            comment.Content,
            comment.CreatedAt);
    }
}
