using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

public interface IProjectTaskCommentApplicationService
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>> GetProjectTaskCommentsAsync(Guid userId, Guid projectId, Guid taskId, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectTaskCommentView>> CreateProjectTaskCommentAsync(CreateProjectTaskCommentCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<bool>> DeleteProjectTaskCommentAsync(Guid userId, Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default);
}