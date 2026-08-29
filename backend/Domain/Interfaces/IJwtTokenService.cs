using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using System.Security.Claims;

namespace Domain.Interfaces
{
    /// <summary>
    /// JWT Token Service interface for token generation and validation
    /// All asynchronous operations accept a cancellation token for request-scoped work.
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Generate both access and refresh tokens for a user
        /// </summary>
        Task<JwtTokens> GenerateTokensAsync(User user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate and get claims from a JWT token
        /// </summary>
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchange a valid refresh token for a new access/refresh pair.
        /// </summary>
        Task<JwtTokens?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revoke a refresh token (add to blacklist)
        /// </summary>
        Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes all active refresh-token sessions for a user.
        /// </summary>
        Task RevokeAllUserTokensAsync(Guid userId, RevocationReason reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a refresh token is revoked
        /// </summary>
        Task<bool> IsTokenRevokedAsync(string refreshToken, CancellationToken cancellationToken = default);
        
    }
}
