using Domain.Enums;

namespace API.Contracts.Projects;

/// <summary>
/// Request payload used by a project owner to create an invitation for an existing active user.
/// </summary>
/// <param name="Email">Email address of the active account that may accept the invitation.</param>
/// <param name="Role">Member or Viewer role granted after acceptance.</param>
public sealed record CreateProjectInvitationRequest(string Email, ProjectMemberRole Role = ProjectMemberRole.Member);

/// <summary>
/// Request payload used by an authenticated invitation recipient to accept or decline a link.
/// </summary>
/// <param name="Token">One-time raw token supplied in the invitation link.</param>
public sealed record RespondToProjectInvitationRequest(string Token);

/// <summary>
/// Invitation details available to the project owner or intended recipient.
/// </summary>
public sealed record ProjectInvitationResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid InvitedUserId,
    string InvitedUserDisplayName,
    string InvitedUserEmail,
    string InvitedByDisplayName,
    ProjectMemberRole Role,
    ProjectInvitationStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

/// <summary>
/// Result returned once to an owner after creating an invitation.
/// </summary>
public sealed record CreatedProjectInvitationResponse(ProjectInvitationResponse Invitation, string Token);
