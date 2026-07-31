namespace Application.DTOs.Auth;

/// <summary>Response returned when login requires a separate email 2FA verification step.</summary>
public class TwoFactorChallengeResponseDto
{
    /// <summary>Always true for this response and indicates that JWT tokens have not been issued yet.</summary>
    public bool RequiresTwoFactor { get; set; } = true;

    /// <summary>Verification method expected for this challenge: <c>email</c> or <c>authenticator</c>.</summary>
    public string Method { get; set; } = "email";

    /// <summary>Identifier submitted with the subsequent 2FA verification request.</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Masked destination hint shown to the user without exposing the full email address.</summary>
    public string DestinationHint { get; set; } = string.Empty;

    /// <summary>UTC time after which the challenge can no longer be verified.</summary>
    public DateTime ExpiresAt { get; set; }
}