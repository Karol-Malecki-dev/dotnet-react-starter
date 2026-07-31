using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A time-limited invitation for one active user to join a project.
/// The raw invitation token is never persisted.
/// </summary>
public class ProjectInvitation
{
    /// <summary>Unique identifier for the invitation.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Project the recipient is invited to join.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Active user who may accept or decline the invitation.</summary>
    public Guid InvitedUserId { get; set; }

    /// <summary>Project owner who created the invitation.</summary>
    public Guid InvitedByUserId { get; set; }

    /// <summary>Role granted if the invitation is accepted.</summary>
    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Member;

    /// <summary>Current lifecycle state of the invitation.</summary>
    public ProjectInvitationStatus Status { get; set; } = ProjectInvitationStatus.Pending;

    /// <summary>SHA-256 hash of the raw, URL-safe invitation token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp at which the invitation becomes invalid.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC timestamp at which the recipient accepted or declined the invitation.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>UTC timestamp at which the invitation was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public User InvitedUser { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}
