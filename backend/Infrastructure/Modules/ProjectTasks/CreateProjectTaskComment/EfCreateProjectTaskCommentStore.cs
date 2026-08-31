using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Application.Interfaces;
using Domain.Enums;
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
    private readonly ICollaborationNotificationWriter? _notificationWriter;

    public EfCreateProjectTaskCommentStore(
        ApplicationDbContext dbContext,
        ICollaborationNotificationWriter? notificationWriter = null)
    {
        _dbContext = dbContext;
        _notificationWriter = notificationWriter;
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
        var assigneeId = await _dbContext.ProjectTasks
            .Where(task => task.Id == command.ProjectTaskId)
            .Select(task => task.AssignedUserId)
            .SingleAsync(cancellationToken);
        if (_notificationWriter is not null && assigneeId is { } recipientId && recipientId != command.AuthorUserId)
        {
            await _notificationWriter.StageAsync(
                recipientId,
                NotificationType.TaskCommented,
                "New task comment",
                "A new comment was added to your assigned task.",
                "projectTask",
                command.ProjectTaskId,
                command.ProjectId,
                $"task:{command.ProjectTaskId}:comment:{comment.Id}",
                cancellationToken);
        }
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
