using Application.Features.Projects;
using Application.Modules.Projects.CreateProject;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.CreateProject;

/// <summary>
/// Coordinates project creation and records the initial project activity.
/// </summary>
public sealed class CreateProjectHandler : ICreateProjectHandler
{
    private readonly ICreateProjectStore _store;

    public CreateProjectHandler(ICreateProjectStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<ProjectView>> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = Project.Create(command.OwnerId, command.Name, command.Description);
        _store.AddProject(project);
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = project.Id,
            ActorUserId = command.OwnerId,
            Type = "project.created",
            Description = $"created the project '{project.Name}'."
        });

        await _store.SaveChangesAsync(cancellationToken);

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
            "Project created",
            201);
    }
}
