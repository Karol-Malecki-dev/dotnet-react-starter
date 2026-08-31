using Application.Features.Projects;
using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;

namespace Application.Modules.Projects.CreateProjectInvitation;

/// <summary>
/// Creates a time-limited invitation for an existing active user.
/// </summary>
public sealed record CreateProjectInvitationCommand(
    Guid OwnerId,
    Guid ProjectId,
    string Email,
    ProjectMemberRole Role);

/// <summary>
/// Contains project data required to create and describe an invitation.
/// </summary>
public sealed record ProjectInvitationCreationContext(
    string ProjectName,
    string InviterDisplayName);

/// <summary>
/// Executes the create-project-invitation use case.
/// </summary>
public interface ICreateProjectInvitationHandler
{
    Task<ProjectOperationResult<CreatedProjectInvitationView>> HandleAsync(
        CreateProjectInvitationCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides persistence operations required by the create-project-invitation slice.
/// </summary>
public interface ICreateProjectInvitationStore
{
    Task<ProjectInvitationCreationContext?> GetOwnedProjectContextAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<User?> GetActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<bool> IsMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns tracked pending invitations so expired records can leave the active uniqueness set.
    /// </summary>
    Task<IReadOnlyList<ProjectInvitation>> GetPendingInvitationsAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);

    void AddInvitation(ProjectInvitation invitation);

    void AddActivity(ProjectActivity activity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
