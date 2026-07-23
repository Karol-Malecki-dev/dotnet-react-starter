using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth
{
    /// <summary>Data required to create a new user account.</summary>
    public class RegisterUserDto
    {
        /// <summary>Email address that must be confirmed before login.</summary>
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Plain-text password validated and hashed by the backend.</summary>
        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        /// <summary>User's first name.</summary>
        [Required]
        [MinLength(2)]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>User's last name.</summary>
        [Required]
        [MinLength(2)]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Optional phone number associated with the account.</summary>
        [Phone]
        [MaxLength(32)]
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>Optional postal address associated with the account.</summary>
        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        /// <summary>Creation timestamp supplied by the client contract and normalized by the backend when needed.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
