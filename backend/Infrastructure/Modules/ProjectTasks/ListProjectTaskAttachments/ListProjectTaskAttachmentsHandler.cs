using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.ListProjectTaskAttachments;

namespace Infrastructure.Modules.ProjectTasks.ListProjectTaskAttachments;

/// <summary>
/// Coordinates access checks and attachment metadata retrieval for the list-attachments slice.
/// </summary>
public sealed class ListProjectTaskAttachmentsHandler : IListProjectTaskAttachmentsHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IListProjectTaskAttachmentsQueryStore _queryStore;

    public ListProjectTaskAttachmentsHandler(
        IProjectTaskAccess projectTaskAccess,
        IListProjectTaskAttachmentsQueryStore queryStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _queryStore = queryStore;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>> HandleAsync(
        ListProjectTaskAttachmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            query.UserId,
            query.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            query.ProjectId,
            query.TaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var attachments = await _queryStore.QueryAsync(query.TaskId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>.Success(attachments);
    }
}
