using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

public class ResendTwoFactorRequestDto
{
    [Required]
    public Guid ChallengeId { get; set; }
}