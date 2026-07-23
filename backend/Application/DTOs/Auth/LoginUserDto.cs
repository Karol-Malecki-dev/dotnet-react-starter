using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    /// <summary>Credentials submitted to start an authenticated session.</summary>
    public class LoginUserDto
    {
        /// <summary>Email address used to identify the account.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Plain-text password received over the protected transport and never persisted.</summary>
        public string Password { get; set; } = string.Empty;
    }
}
