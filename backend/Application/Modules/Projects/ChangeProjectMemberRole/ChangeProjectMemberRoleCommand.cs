using Application.Features.Projects;
using MediatR;
using Domain.Entities;
using Domain.Enums;

namespace Application.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// Changes the role of an existing non-owner project member.
/// </summary>
public sealed record ChangeProjectMemberRoleCommand(
    Guid OwnerId,
    Guid ProjectId,
    Guid UserId,
    ProjectMemberRole Role)
    : IRequest<ProjectOperationResult<ProjectMemberView>>;


/// <summary>
/// Provides persistence operations required by the change-project-member-role slice.
/// </summary>
public interface IChangeProjectMemberRoleStore
{
    Task<Project?> GetOwnedProjectWithMembersAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
