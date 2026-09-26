using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.GetProjectActivity;

namespace Infrastructure.Modules.Projects.GetProjectActivity;

/// <summary>
/// Applies project access and pagination rules to the activity request.
/// </summary>
public sealed class GetProjectActivityHandler : IRequestHandler<GetProjectActivityQuery, ProjectOperationResult<PagedProjectActivityView>>
{
    private readonly IGetProjectActivityStore _store;

    public GetProjectActivityHandler(IGetProjectActivityStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<PagedProjectActivityView>> Handle(
        GetProjectActivityQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!await _store.HasProjectAccessAsync(
                request.UserId,
                request.ProjectId,
                cancellationToken))
        {
            return ProjectOperationResult<PagedProjectActivityView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = await _store.QueryAsync(
            request.ProjectId,
            pageNumber,
            pageSize,
            cancellationToken);
        return ProjectOperationResult<PagedProjectActivityView>.Success(page);
    }
}
