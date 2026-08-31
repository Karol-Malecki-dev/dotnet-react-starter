using Application.Features.Projects;
using Application.Modules.Projects.UpdateProject;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.UpdateProject;

/// <summary>
/// Coordinates owner authorization, project mutation, and optimistic concurrency handling.
/// </summary>
public sealed class UpdateProjectHandler : IUpdateProjectHandler
{
    private const string ConcurrencyConflictMessage = "Project was modified concurrently; refresh and retry";

    private readonly IUpdateProjectStore _store;

    public UpdateProjectHandler(IUpdateProjectStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<ProjectView>> HandleAsync(
        UpdateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectAsync(
            command.OwnerId,
            command.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<ProjectView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        if (project.IsArchived)
        {
            return ProjectOperationResult<ProjectView>.Failure(
                ProjectOperationStatus.Conflict,
                "Archived project cannot be updated");
        }

        if (command.ExpectedConcurrencyStamp is not null
            && !string.Equals(
                project.ConcurrencyStamp,
                command.ExpectedConcurrencyStamp,
                StringComparison.Ordinal))
        {
            return ProjectOperationResult<ProjectView>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        project.Rename(command.Name);
        project.ChangeDescription(command.Description);

        try
        {
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _store.ClearChangeTracker();
            return ProjectOperationResult<ProjectView>.Failure(
                ProjectOperationStatus.Conflict,
                ConcurrencyConflictMessage);
        }

        return ProjectOperationResult<ProjectView>.Success(
            new ProjectView(
                project.Id,
                project.Name,
                project.Description,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.ConcurrencyStamp,
                project.IsArchived,
                ProjectMemberRole.Owner),
            "Project updated");
    }
}
