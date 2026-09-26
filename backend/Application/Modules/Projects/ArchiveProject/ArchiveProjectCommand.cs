using Application.Features.Projects;
using MediatR;
using Domain.Entities;

namespace Application.Modules.Projects.ArchiveProject;

/// <summary>
/// Represents the application input for archiving a project.
/// </summary>
public sealed record ArchiveProjectCommand(Guid OwnerId, Guid ProjectId)
    : IRequest<ProjectOperationResult<bool>>;


/// <summary>
/// Provides the persistence operations required by the archive-project slice.
/// </summary>
public interface IArchiveProjectStore
{
    Task<Project?> GetOwnedProjectAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearChangeTracker();
}
