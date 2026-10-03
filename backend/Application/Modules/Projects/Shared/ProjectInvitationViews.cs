using Domain.Enums;

namespace Application.Modules.Projects.Shared;

/// <summary>Invitation information visible to the project owner or its intended recipient.</summary>
public sealed record ProjectInvitationView(
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

/// <summary>Result of creating an invitation. The raw token is returned once and is never persisted.</summary>
public sealed record CreatedProjectInvitationView(ProjectInvitationView Invitation, string Token);
