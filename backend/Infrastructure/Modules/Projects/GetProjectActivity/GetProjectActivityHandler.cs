using Application.Features.Projects;
using Application.Modules.Projects.GetProjectActivity;

namespace Infrastructure.Modules.Projects.GetProjectActivity;

/// <summary>
/// Applies project access and pagination rules to the activity query.
/// </summary>
public sealed class GetProjectActivityHandler : IGetProjectActivityHandler
{
    private readonly IGetProjectActivityStore _store;

    public GetProjectActivityHandler(IGetProjectActivityStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<PagedProjectActivityView>> HandleAsync(
        GetProjectActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<PagedProjectActivityView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = await _store.QueryAsync(
            query.ProjectId,
            pageNumber,
            pageSize,
            cancellationToken);
        return ProjectOperationResult<PagedProjectActivityView>.Success(page);
    }
}
