using Application.Features.Projects;
using Domain.Entities;

namespace Application.Modules.Projects.RemoveProjectMember;

/// <summary>
/// Removes a non-owner member from a project.
/// </summary>
public sealed record RemoveProjectMemberCommand(Guid OwnerId, Guid ProjectId, Guid UserId);

/// <summary>
/// Executes the remove-project-member use case.
/// </summary>
public interface IRemoveProjectMemberHandler
{
    Task<ProjectOperationResult<bool>> HandleAsync(
        RemoveProjectMemberCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides persistence operations required by the remove-project-member slice.
/// </summary>
public interface IRemoveProjectMemberStore
{
    Task<Project?> GetOwnedProjectWithMembersAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    void RemoveMember(ProjectMember member);

    void AddActivity(ProjectActivity activity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
