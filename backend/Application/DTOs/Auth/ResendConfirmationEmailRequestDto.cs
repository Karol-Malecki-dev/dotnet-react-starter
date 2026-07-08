using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

public class ResendConfirmationEmailRequestDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}