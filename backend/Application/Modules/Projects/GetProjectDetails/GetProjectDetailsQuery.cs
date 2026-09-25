using Application.Features.Projects;
using MediatR;

namespace Application.Modules.Projects.GetProjectDetails;

/// <summary>
/// Requests the details of a project visible to the current user.
/// </summary>
public sealed record GetProjectDetailsQuery(
    Guid UserId,
    Guid ProjectId,
    bool IncludeArchived = false)
    : IRequest<ProjectOperationResult<ProjectView>>;

/// <summary>
/// Provides the persistence projection required by the get-project-details slice.
/// </summary>
public interface IGetProjectDetailsStore
{
    Task<ProjectView?> QueryAsync(
        Guid userId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken = default);
}
