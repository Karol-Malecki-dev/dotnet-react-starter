using Application.Features.Projects;
using MediatR;
using Domain.Entities;

namespace Application.Modules.Projects.AddProjectMember;

/// <summary>
/// Adds an active user to a project owned by the current user.
/// </summary>
public sealed record AddProjectMemberCommand(Guid OwnerId, Guid ProjectId, Guid UserId)
    : IRequest<ProjectOperationResult<ProjectMemberView>>;


/// <summary>
/// Provides the focused persistence operations required by the add-project-member slice.
/// </summary>
public interface IAddProjectMemberStore
{
    Task<Project?> GetOwnedProjectWithMembersAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<User?> GetActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    void AddMember(ProjectMember member);

    void AddActivity(ProjectActivity activity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
