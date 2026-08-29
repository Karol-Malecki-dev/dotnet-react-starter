using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectTaskCommentService : IProjectTaskCommentApplicationService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseProjectTaskCommentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>> GetProjectTaskCommentsAsync(Guid userId, Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!await HasProjectAccessAsync(userId, projectId, cancellationToken) || !await TaskBelongsToProjectAsync(projectId, taskId, cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

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

        var commentViews = comments
            .Select(comment => new ProjectTaskCommentView(
                comment.Id,
                comment.ProjectTaskId,
                comment.AuthorUserId,
                comment.AuthorDisplayName.Value,
                comment.Content,
                comment.CreatedAt))
            .ToList();

        return ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>.Success(commentViews);
    }

    public async Task<ProjectOperationResult<ProjectTaskCommentView>> CreateProjectTaskCommentAsync(CreateProjectTaskCommentCommand command, CancellationToken cancellationToken = default)
    {
        var content = command.Content.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(ProjectOperationStatus.ValidationError, "Comment content must contain between 1 and 2000 characters");
        }

        var role = await GetProjectRoleAsync(command.AuthorUserId, command.ProjectId, cancellationToken);
        if (role is null || !await TaskBelongsToProjectAsync(command.ProjectId, command.ProjectTaskId, cancellationToken))
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(ProjectOperationStatus.Forbidden, "Viewer members cannot add comments");
        }

        var comment = new ProjectTaskComment
        {
            ProjectTaskId = command.ProjectTaskId,
            AuthorUserId = command.AuthorUserId,
            Content = content
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
        return ProjectOperationResult<ProjectTaskCommentView>.Success(
            new ProjectTaskCommentView(comment.Id, comment.ProjectTaskId, comment.AuthorUserId, authorDisplayName.Value, comment.Content, comment.CreatedAt),
            "Project task comment created",
            201);
    }

    public async Task<ProjectOperationResult<bool>> DeleteProjectTaskCommentAsync(Guid userId, Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var role = await GetProjectRoleAsync(userId, projectId, cancellationToken);
        if (role is null || !await TaskBelongsToProjectAsync(projectId, taskId, cancellationToken))
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var comment = await _dbContext.ProjectTaskComments
            .FirstOrDefaultAsync(candidate => candidate.Id == commentId && candidate.ProjectTaskId == taskId, cancellationToken);
        if (comment is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project task comment not found");
        }

        if (role != ProjectMemberRole.Owner && comment.AuthorUserId != userId)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Forbidden, "You cannot delete this comment");
        }

        _dbContext.ProjectTaskComments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ProjectOperationResult<bool>.Success(true, "Project task comment deleted");
    }

    private async Task<ProjectMemberRole?> GetProjectRoleAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId && !candidate.IsArchived, cancellationToken);
        if (project is null) return null;
        if (project.OwnerId == userId) return ProjectMemberRole.Owner;

        return await _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == userId && member.User.IsActive)
            .Select(member => (ProjectMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
        => await GetProjectRoleAsync(userId, projectId, cancellationToken) is not null;

    private Task<bool> TaskBelongsToProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
        => _dbContext.ProjectTasks.AnyAsync(task => task.Id == taskId
            && task.ProjectId == projectId
            && _dbContext.Projects.Any(project => project.Id == projectId && !project.IsArchived), cancellationToken);
}
