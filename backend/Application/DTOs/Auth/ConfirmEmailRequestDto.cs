using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

public class ConfirmEmailRequestDto
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MinLength(16)]
    public string Token { get; set; } = string.Empty;
}