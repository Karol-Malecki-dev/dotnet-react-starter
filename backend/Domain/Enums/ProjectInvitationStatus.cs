namespace Domain.Enums;

/// <summary>Lifecycle state of a project invitation.</summary>
public enum ProjectInvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4
}
