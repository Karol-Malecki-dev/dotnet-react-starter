using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

/// <summary>Request used to verify a pending email or authenticator-app two-factor challenge.</summary>
public class VerifyTwoFactorRequestDto
{
    /// <summary>Identifier of the pending challenge created during login.</summary>
    [Required]
    public Guid ChallengeId { get; set; }

    /// <summary>Code from the selected authenticator method, including a recovery code when applicable.</summary>
    [Required]
    [StringLength(64, MinimumLength = 4)]
    public string Code { get; set; } = string.Empty;
}