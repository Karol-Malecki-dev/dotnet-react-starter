using Application.Features.Projects;
using Application.Modules.Projects.ArchiveProject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ArchiveProject;

/// <summary>
/// Coordinates owner authorization, archive mutation, and optimistic concurrency handling.
/// </summary>
public sealed class ArchiveProjectHandler : IArchiveProjectHandler
{
    private const string ConcurrencyConflictMessage = "Project was modified concurrently; refresh and retry";

    private readonly IArchiveProjectStore _store;

    public ArchiveProjectHandler(IArchiveProjectStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<bool>> HandleAsync(
        ArchiveProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (project.IsArchived)
        {
            return ProjectOperationResult<bool>.Success(true, "Project already archived");
        }

        project.Archive();

        try
        {
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _store.ClearChangeTracker();
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        return ProjectOperationResult<bool>.Success(true, "Project archived");
    }
}
