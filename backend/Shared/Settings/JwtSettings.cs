namespace Shared.Settings
{
    /// <summary>Configuration for JWT creation, validation, and the refresh-token cookie.</summary>
    public class JwtSettings
    {
        /// <summary>Signing secret used to create and validate access tokens.</summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>Expected issuer claim in access tokens.</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>Expected audience claim in access tokens.</summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>Access token expiry. Default: 15 minutes.</summary>
        public int AccessTokenExpiresInMinutes { get; set; } = 15;

        /// <summary>Refresh token expiry. Default: 7 days.</summary>
        public int RefreshTokenExpiresInDays { get; set; } = 7;

        /// <summary>Refresh token cookie name.</summary>
        public string RefreshTokenCookieName { get; set; } = "drs.refreshToken";

        /// <summary>Refresh token cookie path.</summary>
        public string RefreshTokenCookiePath { get; set; } = "/api/auth";

        /// <summary>
        /// SameSite policy for refresh token cookie. Valid values: Strict, Lax, None, Unspecified.
        /// </summary>
        public string RefreshTokenCookieSameSite { get; set; } = "Lax";

        /// <summary>
        /// Secure policy for refresh token cookie. Valid values: Always, SameAsRequest, None.
        /// </summary>
        public string RefreshTokenCookieSecurePolicy { get; set; } = "Always";

        /// <summary>Optional cookie domain, useful for production subdomain deployments.</summary>
        public string? RefreshTokenCookieDomain { get; set; }

        /// <summary>Marks the auth cookie as essential for consent policies.</summary>
        public bool RefreshTokenCookieIsEssential { get; set; } = true;
    }
}
