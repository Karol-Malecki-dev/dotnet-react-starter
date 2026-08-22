using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using System.Security.Claims;

namespace Domain.Interfaces
{
    /// <summary>
    /// JWT Token Service interface for token generation and validation
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Generate both access and refresh tokens for a user
        /// </summary>
        Task<JwtTokens> GenerateTokensAsync(User user);

        /// <summary>
        /// Validate and get claims from a JWT token
        /// </summary>
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

        /// <summary>
        /// Exchange a valid refresh token for a new access/refresh pair.
        /// </summary>
        Task<JwtTokens?> RefreshTokensAsync(string refreshToken);

        /// <summary>
        /// Revoke a refresh token (add to blacklist)
        /// </summary>
        Task RevokeTokenAsync(string refreshToken);

        /// <summary>
        /// Revokes all active refresh-token sessions for a user.
        /// </summary>
        Task RevokeAllUserTokensAsync(Guid userId, RevocationReason reason);

        /// <summary>
        /// Check if a refresh token is revoked
        /// </summary>
        Task<bool> IsTokenRevokedAsync(string refreshToken);
        
    }
}
