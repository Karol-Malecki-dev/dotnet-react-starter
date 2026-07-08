using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Shared.Responses;
using Shared.Settings;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IAuthService _authService;
        private readonly IAccountEmailSender _accountEmailSender;
        private readonly IUserService _userService;
        private readonly EmailConfirmationSettings _emailConfirmationSettings;
        private readonly EmailTwoFactorSettings _emailTwoFactorSettings;
        private readonly ILogger<AuthController> _logger;
        private readonly JwtSettings _jwtSettings;

        public AuthController(
            IJwtTokenService jwtTokenService,
            IAuthService authService,
            IAccountEmailSender accountEmailSender,
            IUserService userService,
            ILogger<AuthController> logger,
            IOptions<JwtSettings> jwtOptions,
            IOptions<EmailConfirmationSettings> emailConfirmationOptions,
            IOptions<EmailTwoFactorSettings> emailTwoFactorOptions)
        {
            _jwtTokenService = jwtTokenService;
            _authService = authService;
            _accountEmailSender = accountEmailSender;
            _userService = userService;
            _logger = logger;
            _jwtSettings = jwtOptions.Value;
            _emailConfirmationSettings = emailConfirmationOptions.Value;
            _emailTwoFactorSettings = emailTwoFactorOptions.Value;
        }

        /// <summary>
        /// Login - Generate JWT tokens
        /// POST /api/auth/login
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid login data", null));

            try
            {
                _logger.LogInformation("🔐 Login attempt for email: {Email}", dto.Email);

                // Authenticate user (verify email and password)
                var user = await _authService.AuthenticateAsync(dto.Email, dto.Password);
                if (user == null)
                {
                    _logger.LogWarning("⚠️ Login failed for email: {Email}", dto.Email);
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid email or password", null));
                }

                if (!user.IsEmailConfirmed)
                {
                    _logger.LogWarning("⚠️ Login blocked for unconfirmed email: {Email}", dto.Email);
                    return StatusCode(403, ApiResponse<object>.Error(403, "Email address is not confirmed", null));
                }

                if (_emailTwoFactorSettings.Enabled && user.IsTwoFactorEnabled)
                {
                    var challenge = await _authService.CreateEmailTwoFactorChallengeAsync(user.Id);
                    if (challenge is null)
                    {
                        _logger.LogError("❌ Two-factor challenge generation failed for user: {UserId}", user.Id);
                        return StatusCode(500, ApiResponse<object>.Error(500, "Two-factor verification could not be started", null));
                    }

                    await _accountEmailSender.SendTwoFactorCodeAsync(
                        challenge.Email,
                        challenge.DisplayName,
                        challenge.Code,
                        challenge.ExpiresAt);

                    _logger.LogInformation("📨 Two-factor challenge created for user: {UserId}", user.Id);

                    return Accepted(ApiResponse<TwoFactorChallengeResponseDto>.Success(
                        CreateTwoFactorChallengeResponse(challenge),
                        "Two-factor verification required. Check your email for the code.",
                        202));
                }

                // Generate JWT tokens
                var tokens = await _jwtTokenService.GenerateTokensAsync(user);
                SetRefreshTokenCookie(tokens.RefreshToken);

                _logger.LogInformation("✓ Login successful for user: {UserId} ({Email})", user.Id, user.Email);

                return Ok(ApiResponse<AuthTokenResponse>.Success(CreateTokenResponse(tokens), "Login successful", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Login error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Register - Create new user account
        /// POST /api/auth/register
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid registration data", null));

            try
            {
                _logger.LogInformation("📝 Registration attempt for email: {Email}", dto.Email);

                // Check if user already exists
                var userExists = await _authService.UserExistsAsync(dto.Email);
                if (userExists)
                {
                    _logger.LogWarning("⚠️ Registration failed: User already exists with email: {Email}", dto.Email);
                    return BadRequest(ApiResponse<object>.Error(400, "User with this email already exists", null));
                }

                // Register user
                var displayName = $"{dto.FirstName} {dto.LastName}".Trim();
                var user = await _authService.RegisterAsync(dto.Email, dto.Password, displayName);
                if (user == null)
                {
                    _logger.LogError("❌ User registration failed for email: {Email}", dto.Email);
                    return StatusCode(500, ApiResponse<object>.Error(500, "User registration failed", null));
                }

                var confirmationToken = await _authService.GenerateEmailConfirmationTokenAsync(user.Id);
                if (string.IsNullOrWhiteSpace(confirmationToken))
                {
                    _logger.LogError("❌ Confirmation token generation failed for user: {UserId}", user.Id);
                    return StatusCode(500, ApiResponse<object>.Error(500, "User registered but confirmation email could not be prepared", null));
                }

                await _accountEmailSender.SendEmailConfirmationAsync(
                    user.Email,
                    user.DisplayName,
                    BuildConfirmationLink(user.Id, confirmationToken));

                _logger.LogInformation("✓ Registration successful for user: {UserId} ({Email})", user.Id, user.Email);

                return Created(
                    $"api/auth/user/{user.Id}",
                    ApiResponse<RegisterUserResultDto>.Success(
                        new RegisterUserResultDto
                        {
                            Email = user.Email,
                            RequiresEmailConfirmation = true
                        },
                        "Registration successful. Check your email to confirm the account.",
                        201));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Registration error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        [HttpPost("confirm-email")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request)
        {
            if (!ModelState.IsValid || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(ApiResponse<object>.Error(400, "Invalid confirmation request", null));

            try
            {
                var confirmed = await _authService.ConfirmEmailAsync(request.UserId, request.Token);
                if (!confirmed)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid or expired confirmation link", null));
                }

                return Ok(ApiResponse<object>.Success(null, "Email confirmed successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Confirm email error for user {UserId}", request.UserId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        [HttpPost("resend-confirmation")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationEmailRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            try
            {
                var userResult = await _userService.GetUserByEmailAsync(request.Email);
                var userId = userResult.Data?.Id;

                if (userId is Guid existingUserId && existingUserId != Guid.Empty)
                {
                    var isConfirmed = await _authService.IsEmailConfirmedAsync(existingUserId);
                    if (!isConfirmed)
                    {
                        var confirmationToken = await _authService.GenerateEmailConfirmationTokenAsync(existingUserId);
                        if (!string.IsNullOrWhiteSpace(confirmationToken) && userResult.Data is not null)
                        {
                            await _accountEmailSender.SendEmailConfirmationAsync(
                                userResult.Data.Email,
                                userResult.Data.DisplayName,
                                BuildConfirmationLink(existingUserId, confirmationToken));
                        }
                    }
                }

                return Ok(ApiResponse<object>.Success(
                    null,
                    "If the account exists and is not yet confirmed, a confirmation email has been sent.",
                    200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Resend confirmation error for {Email}", request.Email);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        [HttpPost("verify-2fa")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequestDto request)
        {
            if (!ModelState.IsValid || request.ChallengeId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(ApiResponse<object>.Error(400, "Invalid two-factor verification request", null));

            try
            {
                var user = await _authService.VerifyEmailTwoFactorChallengeAsync(request.ChallengeId, request.Code);
                if (user is null)
                {
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid or expired two-factor code", null));
                }

                var tokens = await _jwtTokenService.GenerateTokensAsync(user);
                SetRefreshTokenCookie(tokens.RefreshToken);

                return Ok(ApiResponse<AuthTokenResponse>.Success(
                    CreateTokenResponse(tokens),
                    "Two-factor verification successful",
                    200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Verify 2FA error for challenge {ChallengeId}", request.ChallengeId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        [HttpPost("resend-2fa")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequestDto request)
        {
            if (!ModelState.IsValid || request.ChallengeId == Guid.Empty)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid two-factor resend request", null));

            try
            {
                var challenge = await _authService.ResendEmailTwoFactorChallengeAsync(request.ChallengeId);
                if (challenge is null)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid or expired two-factor challenge", null));
                }

                await _accountEmailSender.SendTwoFactorCodeAsync(
                    challenge.Email,
                    challenge.DisplayName,
                    challenge.Code,
                    challenge.ExpiresAt);

                return Ok(ApiResponse<TwoFactorChallengeResponseDto>.Success(
                    CreateTwoFactorChallengeResponse(challenge),
                    "A new verification code has been sent.",
                    200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Resend 2FA error for challenge {ChallengeId}", request.ChallengeId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Refresh Token - Get new access token using refresh token
        /// POST /api/auth/refresh-token
        /// </summary>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies[_jwtSettings.RefreshTokenCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
                return BadRequest(ApiResponse<object>.Error(400, "Refresh token is required", null));

            try
            {
                _logger.LogInformation("🔄 Refresh token request");

                var tokens = await _jwtTokenService.RefreshTokensAsync(refreshToken);
                if (tokens == null)
                {
                    ClearRefreshTokenCookie();
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid or expired refresh token", null));
                }

                SetRefreshTokenCookie(tokens.RefreshToken);

                return Ok(ApiResponse<AuthTokenResponse>.Success(CreateTokenResponse(tokens), "Token refreshed successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Refresh token error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Logout - Revoke refresh token
        /// POST /api/auth/logout
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies[_jwtSettings.RefreshTokenCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
                return BadRequest(ApiResponse<object>.Error(400, "Refresh token is required", null));

            try
            {
                var userId = User.FindFirst("sub")?.Value;
                _logger.LogInformation("🚪 Logout request from user: {UserId}", userId);

                // Revoke refresh token
                await _jwtTokenService.RevokeTokenAsync(refreshToken);
                ClearRefreshTokenCookie();

                _logger.LogInformation("✓ Logout successful for user: {UserId}", userId);

                return Ok(new ApiResponse(200, "Logout successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Logout error");
                return StatusCode(500, ApiResponse.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Get current user info (protected endpoint)
        /// GET /api/auth/me
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!Guid.TryParse(userId, out var currentUserId))
                    return Unauthorized(ApiResponse<object>.Error(401, "User not authenticated", null));

                var userResult = await _userService.GetUserByIdAsync(currentUserId);
                if (userResult.Data is null)
                {
                    return NotFound(ApiResponse<object>.Error(404, "User not found", null));
                }

                var userData = new
                {
                    id = userResult.Data.Id,
                    email = userResult.Data.Email,
                    displayName = userResult.Data.DisplayName,
                    firstName = userResult.Data.FirstName,
                    lastName = userResult.Data.LastName,
                    avatarUrl = userResult.Data.AvatarUrl,
                    role = userResult.Data.Role
                };

                return Ok(ApiResponse<object>.Success(userData, "Current user info", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error retrieving current user");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Verify token - Check if token is valid
        /// POST /api/auth/verify-token
        /// </summary>
        [HttpPost("verify-token")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyToken([FromBody] VerifyTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
                return BadRequest(ApiResponse<object>.Error(400, "Token is required", null));

            try
            {
                _logger.LogInformation("🔍 Token verification request");

                var principal = await _jwtTokenService.ValidateTokenAsync(request.Token);
                if (principal == null)
                {
                    _logger.LogWarning("⚠️ Token validation failed");
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid token", null));
                }

                _logger.LogInformation("✓ Token is valid");

                return Ok(ApiResponse<object>.Success(new { isValid = true }, "Token is valid", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Token verification error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userId, out var currentUserId))
                return Unauthorized(ApiResponse<object>.Error(401, "User not authenticated", null));

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(ApiResponse<object>.Error(400, "Current password and new password are required", null));

            if (request.CurrentPassword == request.NewPassword)
                return BadRequest(ApiResponse<object>.Error(400, "New password must be different from the current password", null));

            try
            {
                var success = await _authService.ChangePasswordAsync(currentUserId, request.CurrentPassword, request.NewPassword);

                if (!success)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Current password is invalid", null));
                }

                return Ok(ApiResponse<object>.Success(null, "Password changed successfully", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Change password error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        private AuthTokenResponse CreateTokenResponse(JwtTokens tokens)
        {
            return new AuthTokenResponse
            {
                AccessToken = tokens.AccessToken,
                ExpiresIn = tokens.ExpiresIn,
                TokenType = tokens.TokenType
            };
        }

        private TwoFactorChallengeResponseDto CreateTwoFactorChallengeResponse(EmailTwoFactorChallengeDelivery challenge)
        {
            return new TwoFactorChallengeResponseDto
            {
                ChallengeId = challenge.ChallengeId,
                DestinationHint = MaskEmail(challenge.Email),
                ExpiresAt = challenge.ExpiresAt
            };
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append(_jwtSettings.RefreshTokenCookieName, refreshToken, CreateRefreshTokenCookieOptions());
        }

        private void ClearRefreshTokenCookie()
        {
            Response.Cookies.Delete(_jwtSettings.RefreshTokenCookieName, CreateRefreshTokenCookieOptions(DateTimeOffset.UnixEpoch));
        }

        private string BuildConfirmationLink(Guid userId, string token)
        {
            var origin = _emailConfirmationSettings.PublicOrigin.TrimEnd('/');
            var path = _emailConfirmationSettings.ConfirmationPath.StartsWith('/')
                ? _emailConfirmationSettings.ConfirmationPath
                : "/" + _emailConfirmationSettings.ConfirmationPath;

            return QueryHelpers.AddQueryString(origin + path, new Dictionary<string, string?>
            {
                ["userId"] = userId.ToString(),
                ["token"] = token
            });
        }

        private static string MaskEmail(string email)
        {
            var parts = email.Split('@', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return email;
            }

            var localPart = parts[0];
            if (localPart.Length <= 2)
            {
                return $"{localPart[0]}***@{parts[1]}";
            }

            return $"{localPart[0]}***{localPart[^1]}@{parts[1]}";
        }

        private CookieOptions CreateRefreshTokenCookieOptions(DateTimeOffset? expires = null)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = _jwtSettings.RefreshTokenCookieIsEssential,
                Expires = expires ?? DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiresInDays),
                Path = _jwtSettings.RefreshTokenCookiePath,
                SameSite = ParseSameSiteMode(_jwtSettings.RefreshTokenCookieSameSite),
                Secure = ResolveSecureFlag(ParseSecurePolicy(_jwtSettings.RefreshTokenCookieSecurePolicy))
            };

            if (!string.IsNullOrWhiteSpace(_jwtSettings.RefreshTokenCookieDomain))
            {
                options.Domain = _jwtSettings.RefreshTokenCookieDomain;
            }

            return options;
        }

        private static SameSiteMode ParseSameSiteMode(string value)
        {
            return Enum.TryParse<SameSiteMode>(value, true, out var parsed)
                ? parsed
                : SameSiteMode.Lax;
        }

        private static CookieSecurePolicy ParseSecurePolicy(string value)
        {
            return Enum.TryParse<CookieSecurePolicy>(value, true, out var parsed)
                ? parsed
                : CookieSecurePolicy.SameAsRequest;
        }

        private bool ResolveSecureFlag(CookieSecurePolicy securePolicy)
        {
            return securePolicy switch
            {
                CookieSecurePolicy.Always => true,
                CookieSecurePolicy.None => false,
                _ => Request.IsHttps,
            };
        }

        // Pasword resets:
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            if (dto.ResetType != Domain.Enums.Auth.ResetType.Link)
                return BadRequest(ApiResponse<object>.Error(400, "Only link-based password reset is currently supported", null));

            try
            {
                _logger.LogInformation("🔑 Forgot password request for email: {Email}", dto.Email);
                var result = await _authService.SendPasswordResetEmailAsync(dto.Email);
                if (!result)
                {
                    return Ok(ApiResponse<object>.Success(
                        null,
                        "If the account exists, a password reset message has been sent.",
                        200));
                }
                _logger.LogInformation("✓ Password reset email sent to: {Email}", dto.Email);
                return Ok(ApiResponse<object>.Success(null, "Password reset email sent", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Forgot password error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }


        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            if (request.ResetType != Domain.Enums.Auth.ResetType.Link)
                return BadRequest(ApiResponse<object>.Error(400, "Only link-based password reset is currently supported", null));

            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(ApiResponse<object>.Error(400, "Reset token is required", null));

            try
            {
                var success = await _authService.ResetPasswordAsync(
                    request.Email,
                    request.Token,
                    request.NewPassword);

                if (!success)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid token or email", null));
                }

                return Ok(ApiResponse<object>.Success(null, "Password reset successful", 200));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Reset password error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }
    }
}
