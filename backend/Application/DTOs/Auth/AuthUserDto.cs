using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Safe user projection returned by authentication endpoints.
    /// It intentionally excludes password and token persistence data.
    /// </summary>
    public class AuthUserDto
    {
        /// <summary>Unique identifier of the authenticated user.</summary>
        public Guid Id { get; set; }

        /// <summary>Normalized email address associated with the account.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Display name shown in the authenticated client.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Application role used for authorization decisions.</summary>
        public string Role { get; set; } = string.Empty;
    }
}
