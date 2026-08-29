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
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role",
                ClockSkew = TimeSpan.Zero
            };
        }

        public async Task<JwtTokens> GenerateTokensAsync(User user, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);

            var tokenPair = CreateTokenPair(user, DateTime.UtcNow, Guid.NewGuid());
            _dbContext.RefreshTokens.Add(tokenPair.RefreshToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated tokens for user {UserId} ({Email}), IP: {Ip}",
                user.Id,
                user.Email.Value,
                tokenPair.RefreshToken.CreatedByIp);

            return tokenPair.Tokens;
        }

        public async Task<JwtTokens?> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

            if (storedToken is null)
            {
                _logger.LogWarning("Refresh token not found");
                return null;
            }

            var now = DateTime.UtcNow;

            if (storedToken.RevokedAt.HasValue)
            {
                if (storedToken.RevocationReason == RevocationReason.TokenRotated
                    && storedToken.ReplacedByTokenHash is not null)
                {
                    await RevokeTokenFamilyAsync(
                        storedToken.FamilyId,
                        now,
                        RevocationReason.RefreshTokenReplay,
                        cancellationToken);
                }

                _logger.LogWarning("Refresh token is revoked for user {UserId}", storedToken.UserId);
                return null;
            }

            if (storedToken.ExpiresAt <= now)
            {
                _logger.LogWarning("Refresh token is expired for user {UserId}", storedToken.UserId);
                return null;
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == storedToken.UserId, cancellationToken);

            if (user is null || !user.IsActive)
            {
                _logger.LogWarning("Refresh token rejected because user {UserId} is missing or inactive", storedToken.UserId);
                return null;
            }

            var clientIp = GetClientIp();
            var familyId = storedToken.FamilyId ?? Guid.NewGuid();
            var tokenPair = CreateTokenPair(user, now, familyId);

            storedToken.LastUsedAt = now;
            storedToken.LastUsedByIp = clientIp;
            storedToken.FamilyId = familyId;
            storedToken.RevokedAt = now;
            storedToken.RevocationReason = RevocationReason.TokenRotated;
            storedToken.ReplacedByTokenHash = tokenPair.RefreshToken.TokenHash;
            storedToken.ConcurrencyStamp = GenerateConcurrencyStamp();
            _dbContext.RefreshTokens.Add(tokenPair.RefreshToken);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _dbContext.ChangeTracker.Clear();

                var latestToken = await _dbContext.RefreshTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

                if (latestToken is not null
                    && latestToken.RevocationReason == RevocationReason.TokenRotated
                    && latestToken.ReplacedByTokenHash is not null)
                {
                    await RevokeTokenFamilyAsync(
                        latestToken.FamilyId,
                        DateTime.UtcNow,
                        RevocationReason.RefreshTokenReplay,
                        cancellationToken);
                }

                _logger.LogWarning("Concurrent refresh rejected for user {UserId}", storedToken.UserId);
                return null;
            }

            return tokenPair.Tokens;
        }

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        public async Task RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

            if (storedToken is null || storedToken.RevokedAt.HasValue)
            {
                return;
            }

            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevocationReason = RevocationReason.UserLogout;
            storedToken.LastUsedAt = DateTime.UtcNow;
            storedToken.LastUsedByIp = GetClientIp();
            storedToken.ConcurrencyStamp = GenerateConcurrencyStamp();

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogInformation("Refresh token was already changed during logout");
                return;
            }

            _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);
        }

        public async Task RevokeAllUserTokensAsync(Guid userId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var activeTokens = await _dbContext.RefreshTokens
                .Where(x => x.UserId == userId && !x.RevokedAt.HasValue)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
                token.RevocationReason = reason;
                token.ConcurrencyStamp = GenerateConcurrencyStamp();
            }

            if (activeTokens.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> IsTokenRevokedAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var tokenHash = HashToken(refreshToken);
            var storedToken = await _dbContext.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

            return storedToken is null
                || storedToken.RevokedAt.HasValue
                || storedToken.ExpiresAt <= DateTime.UtcNow;
        }

        private string GetClientIp()
            => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private (JwtTokens Tokens, RefreshToken RefreshToken) CreateTokenPair(User user, DateTime now, Guid familyId)
        {
            var accessTokenExpiration = now.AddMinutes(_jwtSettings.AccessTokenExpiresInMinutes);
            var rawRefreshToken = GenerateRefreshToken();
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                UserEmail = user.Email.Value,
                UserDisplayName = user.DisplayName.Value,
                UserRole = user.Role,
                IsEmailConfirmed = user.IsEmailConfirmed,
                TokenHash = HashToken(rawRefreshToken),
                ConcurrencyStamp = GenerateConcurrencyStamp(),
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpiresInDays),
                CreatedByIp = GetClientIp(),
                FamilyId = familyId
            };

            return (
                new JwtTokens
                {
                    AccessToken = CreateAccessToken(user, accessTokenExpiration),
                    RefreshToken = rawRefreshToken,
                    ExpiresIn = (long)(accessTokenExpiration - now).TotalSeconds,
                    TokenType = "Bearer"
                },
                refreshToken);
        }

        private async Task RevokeTokenFamilyAsync(Guid? familyId, DateTime revokedAt, RevocationReason reason, CancellationToken cancellationToken)
        {
            if (!familyId.HasValue)
            {
                return;
            }

            var activeTokens = await _dbContext.RefreshTokens
                .Where(x => x.FamilyId == familyId.Value && !x.RevokedAt.HasValue)
                .ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedAt = revokedAt;
                token.RevocationReason = reason;
                token.ConcurrencyStamp = GenerateConcurrencyStamp();
            }

            if (activeTokens.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private string CreateAccessToken(User user, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email.Value),
                new(ClaimTypes.Name, user.DisplayName.Value),
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

        private static string GenerateConcurrencyStamp()
            => Guid.NewGuid().ToString("N");
    }
}
