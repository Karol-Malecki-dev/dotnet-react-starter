using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

/// <summary>
/// Request used to consume an email confirmation token for a user account.
/// </summary>
public class ConfirmEmailRequestDto
{
    /// <summary>Identifier of the user whose email address is being confirmed.</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Raw, single-use confirmation token received in the email link.</summary>
    [Required]
    [MinLength(16)]
    public string Token { get; set; } = string.Empty;
}