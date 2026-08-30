using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.ListProjectTaskComments;

namespace Infrastructure.Modules.ProjectTasks.ListProjectTaskComments;

/// <summary>
/// Coordinates access checks and comment retrieval for the list-comments slice.
/// </summary>
public sealed class ListProjectTaskCommentsHandler : IListProjectTaskCommentsHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IListProjectTaskCommentsQueryStore _queryStore;

    public ListProjectTaskCommentsHandler(
        IProjectTaskAccess projectTaskAccess,
        IListProjectTaskCommentsQueryStore queryStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _queryStore = queryStore;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>> HandleAsync(
        ListProjectTaskCommentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            query.UserId,
            query.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            query.ProjectId,
            query.TaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var comments = await _queryStore.QueryAsync(query.TaskId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>.Success(comments);
    }
}
