using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

public class VerifyTwoFactorRequestDto
{
    [Required]
    public Guid ChallengeId { get; set; }

    [Required]
    [StringLength(10, MinimumLength = 4)]
    public string Code { get; set; } = string.Empty;
}