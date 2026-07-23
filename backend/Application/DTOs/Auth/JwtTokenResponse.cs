using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Internal token pair containing the raw access and refresh token values.
    /// The refresh token is transferred only to the cookie-writing boundary and is not persisted as plain text.
    /// </summary>
    public class JwtTokenResponse
    {
        /// <summary>
        /// Access token (JWT) used for API requests.
        /// </summary>
        public required string AccessToken { get; set; }

        /// <summary>
        /// Raw refresh token used to obtain a new access token.
        /// </summary>
        public required string RefreshToken { get; set; }

        /// <summary>
        /// Access token expiration time in seconds from now.
        /// </summary>
        public required long ExpiresIn { get; set; }

        /// <summary>
        /// Token type used in the Authorization header.
        /// </summary>
        public string TokenType { get; set; } = "Bearer";
    }
}
