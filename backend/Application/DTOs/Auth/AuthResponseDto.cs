using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Legacy authentication response containing an operation result, optional message, token, and user snapshot.
    /// Prefer the more specific authentication response DTOs for new endpoints.
    /// </summary>
    public class AuthResponseDto
    {
        /// <summary>Indicates whether the requested authentication operation succeeded.</summary>
        public bool Success { get; set; }

        /// <summary>Human-readable status or error message safe to expose to the client.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Optional access token returned by legacy authentication flows.</summary>
        public string? Token { get; set; }

        /// <summary>Optional authenticated user snapshot returned with the response.</summary>
        public AuthUserDto? User { get; set; }
    }
}
