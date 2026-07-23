namespace Domain.ValueObjects;

/// <summary>
/// Internal delivery payload for an email-based two-factor challenge.
/// </summary>
/// <remarks>
/// The raw code is included only so the email sender can deliver it immediately.
/// This record must not be returned directly from a public API response or persisted.
/// </remarks>
public sealed record EmailTwoFactorChallengeDelivery(
    /// <summary>Identifier of the pending challenge.</summary>
    Guid ChallengeId,
    /// <summary>Identifier of the user completing sign-in.</summary>
    Guid UserId,
    /// <summary>Recipient email address.</summary>
    string Email,
    /// <summary>Recipient display name.</summary>
    string DisplayName,
    /// <summary>Raw short-lived code sent by email.</summary>
    string Code,
    /// <summary>UTC time after which the code is invalid.</summary>
    DateTime ExpiresAt);