using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using Shared.Helpers;
using Shared.Settings;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TokenValidationParameters _validationParameters;

        public JwtTokenService(
            IOptions<JwtSettings> jwtOptions,
            ApplicationDbContext dbContext,
            ILogger<JwtTokenService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
            _dbContext = dbContext;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;

            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                ClockSkew = TimeSpan.Zero
            };
        }

        public async Task<JwtTokens> GenerateTokensAsync(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var now = DateTime.UtcNow;
            var accessTokenExpiration = now.AddMinutes(_jwtSettings.AccessTokenExpiresInMinutes);
            var accessTokenString = CreateAccessToken(user, accessTokenExpiration);

            var rawRefreshToken = GenerateRefreshToken();
            var refreshTokenHash = HashToken(rawRefreshToken);
            var clientIp = GetClientIp();

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserEmail = user.Email,
                UserDisplayName = user.DisplayName,
                UserRole = user.Role,
                IsEmailConfirmed = user.IsEmailConfirmed,
                TokenHash = refreshTokenHash,
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpiresInDays),
                CreatedByIp = clientIp,
                FamilyId = Guid.NewGuid()
            });

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Generated tokens for user {UserId} ({Email}), IP: {Ip}",
                user.Id,
                user.Email,
                clientIp);

            return new JwtTokens
            {
                AccessToken = accessTokenString,
                RefreshToken = rawRefreshToken,
                ExpiresIn = (long)(accessTokenExpiration - now).TotalSeconds,
                TokenType = "Bearer"
            };
        }

        public async Task<JwtTokens?> RefreshTokensAsync(string refreshToken)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (storedToken is null)
            {
                _logger.LogWarning("Refresh token not found");
                return null;
            }

            if (storedToken.RevokedAt.HasValue || storedToken.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token is expired or revoked for user {UserId}", storedToken.UserId);
                return null;
            }

            var clientIp = GetClientIp();

            storedToken.LastUsedAt = DateTime.UtcNow;
            storedToken.LastUsedByIp = clientIp;

            var newTokens = await GenerateTokensAsync(new User
            {
                Id = storedToken.UserId,
                Email = storedToken.UserEmail,
                DisplayName = storedToken.UserDisplayName,
                Role = storedToken.UserRole,
                IsEmailConfirmed = storedToken.IsEmailConfirmed,
                IsActive = true,
                PasswordHash = string.Empty,
                CreatedAt = storedToken.CreatedAt
            });

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevocationReason = RevocationReason.TokenRotated;
            storedToken.ReplacedByTokenHash = HashToken(newTokens.RefreshToken);

            await _dbContext.SaveChangesAsync();

            return newTokens;
        }

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, _validationParameters, out _);
                return Task.FromResult<ClaimsPrincipal?>(principal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
        }

        public async Task RevokeTokenAsync(string refreshToken)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

            if (storedToken is null || storedToken.RevokedAt.HasValue)
            {
                return;
            }

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevocationReason = RevocationReason.UserLogout;
            storedToken.LastUsedAt = DateTime.UtcNow;
            storedToken.LastUsedByIp = GetClientIp();

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);
        }

        public async Task<bool> IsTokenRevokedAsync(string refreshToken)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);

            return storedToken is null
                || storedToken.RevokedAt.HasValue
                || storedToken.ExpiresAt <= DateTime.UtcNow;
        }

        private string GetClientIp()
            => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private string CreateAccessToken(User user, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("IsEmailConfirmed", user.IsEmailConfirmed.ToString())
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = credentials
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private static string HashToken(string token)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hashBytes);
        }
    }
}
