namespace Application.Features.Projects;

public interface IProjectTaskCommentApplicationService
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>> GetProjectTaskCommentsAsync(Guid userId, Guid projectId, Guid taskId);
    Task<ProjectOperationResult<ProjectTaskCommentView>> CreateProjectTaskCommentAsync(CreateProjectTaskCommentCommand command);
    Task<ProjectOperationResult<bool>> DeleteProjectTaskCommentAsync(Guid userId, Guid projectId, Guid taskId, Guid commentId);
}
