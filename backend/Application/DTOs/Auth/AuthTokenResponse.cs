using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Public authentication token response sent to the frontend after successful authentication.
    /// </summary>
    public class AuthTokenResponse
    {
        /// <summary>Short-lived access token used in the Bearer authorization header.</summary>
        public required string AccessToken { get; set; }

        /// <summary>Access token lifetime in seconds.</summary>
        public required long ExpiresIn { get; set; }

        /// <summary>Authentication scheme used with the access token.</summary>
        public string TokenType { get; set; } = "Bearer";
    }
}
