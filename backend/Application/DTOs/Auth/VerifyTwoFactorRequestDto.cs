using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

/// <summary>Request used to verify a pending email-based two-factor challenge.</summary>
public class VerifyTwoFactorRequestDto
{
    /// <summary>Identifier of the pending challenge created during login.</summary>
    [Required]
    public Guid ChallengeId { get; set; }

    /// <summary>Short-lived code received by the user through email.</summary>
    [Required]
    [StringLength(10, MinimumLength = 4)]
    public string Code { get; set; } = string.Empty;
}