using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
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
    /// <summary>
    /// Exposes account authentication, verification, password-management,
    /// and refresh-token session endpoints.
    /// </summary>
    public class AuthController : ControllerBase
    {
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IAuthService _authService;
        private readonly IAccountEmailSender _accountEmailSender;
        private readonly INotificationService? _notificationService;
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
            IOptions<EmailTwoFactorSettings> emailTwoFactorOptions,
            INotificationService? notificationService = null)
        {
            _jwtTokenService = jwtTokenService;
            _authService = authService;
            _accountEmailSender = accountEmailSender;
            _userService = userService;
            _logger = logger;
            _jwtSettings = jwtOptions.Value;
            _emailConfirmationSettings = emailConfirmationOptions.Value;
            _emailTwoFactorSettings = emailTwoFactorOptions.Value;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Authenticates a user and either issues JWT tokens or starts an email 2FA challenge.
        /// </summary>
        /// <param name="dto">Login email address and password.</param>
        /// <returns>
        /// <see cref="AuthTokenResponse"/> with an access token when 2FA is not required,
        /// or <see cref="TwoFactorChallengeResponseDto"/> with a challenge identifier when 2FA is required.
        /// </returns>
        /// <response code="200">Credentials are valid and JWT tokens were issued. Sets the refresh-token cookie.</response>
        /// <response code="202">Credentials are valid, but email 2FA verification is required. No refresh-token cookie is issued yet.</response>
        /// <response code="400">The request model is invalid.</response>
        /// <response code="401">The email or password is invalid.</response>
        /// <response code="403">The account email has not been confirmed.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Login([FromBody] LoginUserDto dto, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid login data", null));

            try
            {
                _logger.LogInformation("🔐 Login attempt for email: {Email}", dto.Email);

                // Authenticate user (verify email and password)
                var user = await _authService.AuthenticateAsync(dto.Email, dto.Password, cancellationToken);
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

                if (user.IsAuthenticatorEnabled)
                {
                    var challenge = await _authService.CreateAuthenticatorLoginChallengeAsync(user.Id, cancellationToken);
                    if (challenge is null)
                    {
                        _logger.LogError("Authenticator challenge generation failed for user: {UserId}", user.Id);
                        return StatusCode(500, ApiResponse<object>.Error(500, "Two-factor verification could not be started", null));
                    }

                    return Accepted(ApiResponse<TwoFactorChallengeResponseDto>.Success(
                        CreateAuthenticatorChallengeResponse(challenge),
                        "Two-factor verification required. Enter a code from your authenticator app.",
                        202));
                }

                if (_emailTwoFactorSettings.Enabled && user.IsTwoFactorEnabled)
                {
                    var challenge = await _authService.CreateEmailTwoFactorChallengeAsync(user.Id, cancellationToken);
                    if (challenge is null)
                    {
                        _logger.LogError("❌ Two-factor challenge generation failed for user: {UserId}", user.Id);
                        return StatusCode(500, ApiResponse<object>.Error(500, "Two-factor verification could not be started", null));
                    }

                    await _accountEmailSender.SendTwoFactorCodeAsync(
                        challenge.Email,
                        challenge.DisplayName,
                        challenge.Code,
                        challenge.ExpiresAt,
                        cancellationToken);

                    _logger.LogInformation("📨 Two-factor challenge created for user: {UserId}", user.Id);

                    return Accepted(ApiResponse<TwoFactorChallengeResponseDto>.Success(
                        CreateTwoFactorChallengeResponse(challenge),
                        "Two-factor verification required. Check your email for the code.",
                        202));
                }

                // Generate JWT tokens
                var tokens = await _jwtTokenService.GenerateTokensAsync(user, cancellationToken);
                SetRefreshTokenCookie(tokens.RefreshToken);

                _logger.LogInformation("✓ Login successful for user: {UserId} ({Email})", user.Id, user.Email.Value);

                return Ok(ApiResponse<AuthTokenResponse>.Success(CreateTokenResponse(tokens), "Login successful", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Login error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Registers a new user account and sends an email confirmation message.
        /// </summary>
        /// <param name="dto">Registration email address, password, first name, and last name.</param>
        /// <returns><see cref="RegisterUserResultDto"/> describing the newly registered account.</returns>
        /// <response code="201">The account was created. The email requires confirmation before login.</response>
        /// <response code="400">The request is invalid or an account with the email already exists.</response>
        /// <response code="500">The account or confirmation message could not be prepared.</response>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> Register([FromBody] RegisterUserDto dto, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid registration data", null));

            try
            {
                _logger.LogInformation("📝 Registration attempt for email: {Email}", dto.Email);

                // Check if user already exists
                var userExists = await _authService.UserExistsAsync(dto.Email, cancellationToken);
                if (userExists)
                {
                    _logger.LogWarning("⚠️ Registration failed: User already exists with email: {Email}", dto.Email);
                    return BadRequest(ApiResponse<object>.Error(400, "User with this email already exists", null));
                }

                // Register user
                var displayName = $"{dto.FirstName} {dto.LastName}".Trim();
                var user = await _authService.RegisterAsync(dto.Email, dto.Password, displayName, cancellationToken);
                if (user == null)
                {
                    _logger.LogError("❌ User registration failed for email: {Email}", dto.Email);
                    return StatusCode(500, ApiResponse<object>.Error(500, "User registration failed", null));
                }

                var confirmationToken = await _authService.GenerateEmailConfirmationTokenAsync(user.Id, cancellationToken);
                if (string.IsNullOrWhiteSpace(confirmationToken))
                {
                    _logger.LogError("❌ Confirmation token generation failed for user: {UserId}", user.Id);
                    return StatusCode(500, ApiResponse<object>.Error(500, "User registered but confirmation email could not be prepared", null));
                }

                await _accountEmailSender.SendEmailConfirmationAsync(
                    user.Email.Value,
                    user.DisplayName,
                    BuildConfirmationLink(user.Id, confirmationToken),
                    cancellationToken);

                _logger.LogInformation("✓ Registration successful for user: {UserId} ({Email})", user.Id, user.Email.Value);

                return Created(
                    $"api/auth/user/{user.Id}",
                    ApiResponse<RegisterUserResultDto>.Success(
                        new RegisterUserResultDto
                        {
                            Email = user.Email.Value,
                            RequiresEmailConfirmation = true
                        },
                        "Registration successful. Check your email to confirm the account.",
                        201));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Registration error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Confirms a user's email address using a single-use confirmation token.
        /// </summary>
        /// <param name="request">User identifier and raw confirmation token from the email link.</param>
        /// <returns>An empty successful response when the email is confirmed.</returns>
        /// <response code="200">The email was confirmed.</response>
        /// <response code="400">The request is invalid or the token is missing, expired, revoked, or already consumed.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        [HttpPost("confirm-email")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(ApiResponse<object>.Error(400, "Invalid confirmation request", null));

            try
            {
                var confirmed = await _authService.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);
                if (!confirmed)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid or expired confirmation link", null));
                }

                return Ok(ApiResponse<object?>.Success(null, "Email confirmed successfully", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Confirm email error for user {UserId}", request.UserId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Resends an email confirmation message when the account exists and is not confirmed.
        /// </summary>
        /// <param name="request">Email address for the account requiring confirmation.</param>
        /// <returns>A neutral response that does not reveal whether the account exists.</returns>
        /// <response code="200">The request was processed. A message may have been sent.</response>
        /// <response code="400">The request model is invalid.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>This endpoint is anonymous and rate limited to reduce account-enumeration and email-abuse risks.</remarks>
        [HttpPost("resend-confirmation")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationEmailRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            try
            {
                var userResult = await _userService.GetUserByEmailAsync(request.Email, cancellationToken);
                var userId = userResult.Data?.Id;

                if (userId is Guid existingUserId && existingUserId != Guid.Empty)
                {
                    var isConfirmed = await _authService.IsEmailConfirmedAsync(existingUserId, cancellationToken);
                    if (!isConfirmed)
                    {
                        var confirmationToken = await _authService.GenerateEmailConfirmationTokenAsync(existingUserId, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(confirmationToken) && userResult.Data is not null)
                        {
                            await _accountEmailSender.SendEmailConfirmationAsync(
                                userResult.Data.Email,
                                userResult.Data.DisplayName,
                                BuildConfirmationLink(existingUserId, confirmationToken),
                                cancellationToken);
                        }
                    }
                }

                return Ok(ApiResponse<object?>.Success(
                    null,
                    "If the account exists and is not yet confirmed, a confirmation email has been sent.",
                    200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Resend confirmation error for {Email}", request.Email);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Verifies an email 2FA code and completes the login flow.
        /// </summary>
        /// <param name="request">Pending challenge identifier and raw code received by email.</param>
        /// <returns><see cref="AuthTokenResponse"/> when the challenge is valid.</returns>
        /// <response code="200">The code is valid and JWT tokens were issued. Sets the refresh-token cookie.</response>
        /// <response code="400">The request is invalid.</response>
        /// <response code="401">The code is invalid, expired, revoked, or already consumed.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>This endpoint is anonymous because the user has not received an authenticated session yet.</remarks>
        [HttpPost("verify-2fa")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || request.ChallengeId == Guid.Empty || string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(ApiResponse<object>.Error(400, "Invalid two-factor verification request", null));

            try
            {
                var user = await _authService.VerifyAuthenticatorLoginChallengeAsync(request.ChallengeId, request.Code, cancellationToken)
                    ?? await _authService.VerifyEmailTwoFactorChallengeAsync(request.ChallengeId, request.Code, cancellationToken);
                if (user is null)
                {
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid or expired two-factor code", null));
                }

                var tokens = await _jwtTokenService.GenerateTokensAsync(user, cancellationToken);
                SetRefreshTokenCookie(tokens.RefreshToken);

                return Ok(ApiResponse<AuthTokenResponse>.Success(
                    CreateTokenResponse(tokens),
                    "Two-factor verification successful",
                    200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Verify 2FA error for challenge {ChallengeId}", request.ChallengeId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Replaces the code for an active email 2FA challenge and sends the new code.
        /// </summary>
        /// <param name="request">Identifier of the pending 2FA challenge.</param>
        /// <returns><see cref="TwoFactorChallengeResponseDto"/> containing the masked destination and expiration.</returns>
        /// <response code="200">A new code was generated and sent.</response>
        /// <response code="400">The request is invalid or the challenge is expired, revoked, or unavailable.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>The raw code is sent by email and is never included in the API response.</remarks>
        [HttpPost("resend-2fa")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || request.ChallengeId == Guid.Empty)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid two-factor resend request", null));

            try
            {
                var challenge = await _authService.ResendEmailTwoFactorChallengeAsync(request.ChallengeId, cancellationToken);
                if (challenge is null)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid or expired two-factor challenge", null));
                }

                await _accountEmailSender.SendTwoFactorCodeAsync(
                    challenge.Email,
                    challenge.DisplayName,
                    challenge.Code,
                    challenge.ExpiresAt,
                    cancellationToken);

                return Ok(ApiResponse<TwoFactorChallengeResponseDto>.Success(
                    CreateTwoFactorChallengeResponse(challenge),
                    "A new verification code has been sent.",
                    200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Resend 2FA error for challenge {ChallengeId}", request.ChallengeId);
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>Starts authenticator-app setup and returns the provisioning URI exactly once per request.</summary>
        [HttpPost("authenticator/setup")]
        [Authorize]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> BeginAuthenticatorSetup(CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponse<AuthenticatorSetupDto>.Error(401, "User not authenticated"));
            }

            var setup = await _authService.BeginAuthenticatorSetupAsync(userId, cancellationToken);
            if (setup is null)
            {
                return BadRequest(ApiResponse<AuthenticatorSetupDto>.Error(400, "Authenticator setup is unavailable for this account"));
            }

            return Ok(ApiResponse<AuthenticatorSetupDto>.Success(new AuthenticatorSetupDto
            {
                SharedKey = setup.SharedKey,
                ProvisioningUri = setup.ProvisioningUri
            }, "Authenticator setup started", 200));
        }

        /// <summary>Confirms an authenticator-app setup and returns one-time recovery codes.</summary>
        [HttpPost("authenticator/confirm")]
        [Authorize]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ConfirmAuthenticatorSetup([FromBody] ConfirmAuthenticatorSetupRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || !TryGetCurrentUserId(out var userId))
            {
                return BadRequest(ApiResponse<AuthenticatorConfirmationDto>.Error(400, "Invalid authenticator confirmation request"));
            }

            var confirmation = await _authService.ConfirmAuthenticatorSetupAsync(userId, request.Code, cancellationToken);
            if (confirmation is null)
            {
                return BadRequest(ApiResponse<AuthenticatorConfirmationDto>.Error(400, "Authenticator code is invalid"));
            }

            await CreateSecurityAlertAsync(userId, "Authenticator enabled", "Your authenticator app was enabled and recovery codes were created.", cancellationToken);
            return Ok(ApiResponse<AuthenticatorConfirmationDto>.Success(new AuthenticatorConfirmationDto
            {
                RecoveryCodes = confirmation.RecoveryCodes
            }, "Authenticator enabled. Store the recovery codes securely.", 200));
        }

        /// <summary>Disables an authenticator application after validating a current or recovery code.</summary>
        [HttpPost("authenticator/disable")]
        [Authorize]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> DisableAuthenticator([FromBody] DisableAuthenticatorRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || !TryGetCurrentUserId(out var userId))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Invalid authenticator disable request", null));
            }

            if (!await _authService.DisableAuthenticatorAsync(userId, request.CurrentPassword, request.Code, cancellationToken))
            {
                return BadRequest(ApiResponse<object>.Error(400, "Password or authenticator code is invalid", null));
            }

            await CreateSecurityAlertAsync(userId, "Authenticator disabled", "Your authenticator app was disabled after password re-authentication.", cancellationToken);
            return Ok(ApiResponse<object?>.Success(null, "Authenticator disabled", 200));
        }

        /// <summary>Regenerates all recovery codes after password re-authentication and a second-factor code check.</summary>
        [HttpPost("authenticator/recovery-codes")]
        [Authorize]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> RegenerateAuthenticatorRecoveryCodes([FromBody] RegenerateAuthenticatorRecoveryCodesRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || !TryGetCurrentUserId(out var userId))
            {
                return BadRequest(ApiResponse<AuthenticatorConfirmationDto>.Error(400, "Invalid recovery-code regeneration request"));
            }

            var confirmation = await _authService.RegenerateAuthenticatorRecoveryCodesAsync(userId, request.CurrentPassword, request.Code, cancellationToken);
            if (confirmation is null)
            {
                return BadRequest(ApiResponse<AuthenticatorConfirmationDto>.Error(400, "Password or authenticator code is invalid"));
            }

            await CreateSecurityAlertAsync(userId, "Recovery codes regenerated", "Your authenticator recovery codes were replaced after password re-authentication.", cancellationToken);
            return Ok(ApiResponse<AuthenticatorConfirmationDto>.Success(new AuthenticatorConfirmationDto
            {
                RecoveryCodes = confirmation.RecoveryCodes
            }, "Recovery codes regenerated. Store them securely.", 200));
        }

        /// <summary>
        /// Rotates the refresh token and issues a new access token.
        /// </summary>
        /// <returns><see cref="AuthTokenResponse"/> containing the new access token.</returns>
        /// <response code="200">The refresh token was valid and a new token pair was issued. Replaces the refresh-token cookie.</response>
        /// <response code="400">The refresh-token cookie is missing.</response>
        /// <response code="401">The refresh token is invalid or expired. Deletes the refresh-token cookie.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>The endpoint is anonymous because the refresh token is read from the HttpOnly cookie.</remarks>
        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken = default)
        {
            var refreshToken = Request.Cookies[_jwtSettings.RefreshTokenCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
                return BadRequest(ApiResponse<object>.Error(400, "Refresh token is required", null));

            try
            {
                _logger.LogInformation("🔄 Refresh token request");

                var tokens = await _jwtTokenService.RefreshTokensAsync(refreshToken, cancellationToken);
                if (tokens == null)
                {
                    ClearRefreshTokenCookie();
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid or expired refresh token", null));
                }

                SetRefreshTokenCookie(tokens.RefreshToken);

                return Ok(ApiResponse<AuthTokenResponse>.Success(CreateTokenResponse(tokens), "Token refreshed successfully", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Refresh token error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Revokes the current refresh token and removes its cookie.
        /// </summary>
        /// <returns>An empty successful response after revocation.</returns>
        /// <response code="200">The refresh token was revoked and the cookie was deleted.</response>
        /// <response code="400">The refresh-token cookie is missing.</response>
        /// <response code="401">The caller is not authenticated.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>Requires a valid JWT access token and does not require a separate email-confirmation or 2FA check.</remarks>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
        {
            var refreshToken = Request.Cookies[_jwtSettings.RefreshTokenCookieName];
            if (string.IsNullOrWhiteSpace(refreshToken))
                return BadRequest(ApiResponse<object>.Error(400, "Refresh token is required", null));

            try
            {
                var userId = User.FindFirst("sub")?.Value;
                _logger.LogInformation("🚪 Logout request from user: {UserId}", userId);

                // Revoke refresh token
                await _jwtTokenService.RevokeTokenAsync(refreshToken, cancellationToken);
                ClearRefreshTokenCookie();

                _logger.LogInformation("✓ Logout successful for user: {UserId}", userId);

                return Ok(new ApiResponse(200, "Logout successful"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Logout error");
                return StatusCode(500, ApiResponse.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Revokes every active refresh-token session for the authenticated user.
        /// </summary>
        /// <returns>An empty successful response after all sessions are revoked.</returns>
        /// <response code="200">All active refresh-token sessions were revoked and the current cookie was deleted.</response>
        /// <response code="401">The caller is not authenticated or the token does not identify a valid user.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>The access token remains valid until its normal expiration because access-token revocation is not persisted by this slice.</remarks>
        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Unauthorized(ApiResponse<object>.Error(401, "User not authenticated", null));
            }

            try
            {
                _logger.LogInformation("🚪 Logout-all request from user: {UserId}", userId);

                await _jwtTokenService.RevokeAllUserTokensAsync(userId, RevocationReason.UserLogout, cancellationToken);
                ClearRefreshTokenCookie();

                _logger.LogInformation("✓ All refresh sessions revoked for user: {UserId}", userId);
                return Ok(new ApiResponse(200, "All sessions logged out successfully"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Logout-all error for user {UserId}", userId);
                return StatusCode(500, ApiResponse.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Returns the profile of the currently authenticated user.
        /// </summary>
        /// <returns>Current user profile data without credential or token secrets.</returns>
        /// <response code="200">The current user was returned.</response>
        /// <response code="401">The access token is missing, invalid, or does not contain a valid user identifier.</response>
        /// <response code="404">The authenticated user no longer exists.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!Guid.TryParse(userId, out var currentUserId))
                    return Unauthorized(ApiResponse<object>.Error(401, "User not authenticated", null));

                var userResult = await _userService.GetUserByIdAsync(currentUserId, cancellationToken);
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
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Error retrieving current user");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Validates an access token and reports whether it is structurally and cryptographically valid.
        /// </summary>
        /// <param name="request">Access token to validate.</param>
        /// <returns>An object containing <c>isValid</c> when validation succeeds.</returns>
        /// <response code="200">The token is valid.</response>
        /// <response code="400">The token is missing from the request.</response>
        /// <response code="401">The token is invalid or expired.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        [HttpPost("verify-token")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> VerifyToken([FromBody] VerifyTokenRequest request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request?.Token))
                return BadRequest(ApiResponse<object>.Error(400, "Token is required", null));

            try
            {
                _logger.LogInformation("🔍 Token verification request");

                var principal = await _jwtTokenService.ValidateTokenAsync(request.Token, cancellationToken);
                if (principal == null)
                {
                    _logger.LogWarning("⚠️ Token validation failed");
                    return Unauthorized(ApiResponse<object>.Error(401, "Invalid token", null));
                }

                _logger.LogInformation("✓ Token is valid");

                return Ok(ApiResponse<object>.Success(new { isValid = true }, "Token is valid", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Token verification error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }

        /// <summary>
        /// Changes the password of the currently authenticated user.
        /// </summary>
        /// <param name="request">Current password and replacement password.</param>
        /// <returns>An empty successful response after the password is changed.</returns>
        /// <response code="200">The password was changed.</response>
        /// <response code="400">The request is invalid, the passwords are equal, or the current password is incorrect.</response>
        /// <response code="401">The caller is not authenticated or the token does not identify a valid user.</response>
        /// <response code="429">Too many password-change attempts were made.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>Requires a valid JWT access token and removes the current refresh-token cookie after success.</remarks>
        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
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
                var success = await _authService.ChangePasswordAsync(currentUserId, request.CurrentPassword, request.NewPassword, cancellationToken);

                if (!success)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Current password is invalid", null));
                }

                ClearRefreshTokenCookie();
                return Ok(ApiResponse<object?>.Success(null, "Password changed successfully", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
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

        private static TwoFactorChallengeResponseDto CreateAuthenticatorChallengeResponse(AuthenticatorLoginChallengeInfo challenge)
        {
            return new TwoFactorChallengeResponseDto
            {
                Method = "authenticator",
                ChallengeId = challenge.ChallengeId,
                DestinationHint = "your authenticator app",
                ExpiresAt = challenge.ExpiresAt
            };
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdValue, out userId);
        }

        private async Task CreateSecurityAlertAsync(Guid userId, string title, string message, CancellationToken cancellationToken)
        {
            if (_notificationService is null)
            {
                return;
            }

            try
            {
                await _notificationService.CreateAsync(userId, Domain.Enums.NotificationType.SecurityAlert, title, message, "authenticator", cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Could not create authenticator security alert for user {UserId}", userId);
            }
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

        private string BuildPasswordResetLink(string email, string token)
        {
            var origin = _emailConfirmationSettings.PublicOrigin.TrimEnd('/');
            var path = _emailConfirmationSettings.PasswordResetPath.StartsWith('/')
                ? _emailConfirmationSettings.PasswordResetPath
                : "/" + _emailConfirmationSettings.PasswordResetPath;

            return QueryHelpers.AddQueryString(origin + path, new Dictionary<string, string?>
            {
                ["email"] = email,
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

        // Password resets:
        /// <summary>
        /// Starts the password-reset flow for an email address.
        /// </summary>
        /// <param name="dto">Email address and requested reset mechanism.</param>
        /// <returns>A response that normally does not reveal whether the account exists.</returns>
        /// <response code="200">The request was processed. A reset message may have been sent.</response>
        /// <response code="400">The request is invalid or the selected reset mechanism is unsupported.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>This endpoint currently supports link-based reset only and does not set a refresh-token cookie.</remarks>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Error(400, "Invalid request data", null));

            if (dto.ResetType != Domain.Enums.Auth.ResetType.Link)
                return BadRequest(ApiResponse<object>.Error(400, "Only link-based password reset is currently supported", null));

            try
            {
                _logger.LogInformation("🔑 Forgot password request for email: {Email}", dto.Email);
                var resetToken = await _authService.GeneratePasswordResetTokenAsync(dto.Email, cancellationToken);
                if (resetToken is null)
                {
                    return Ok(ApiResponse<object?>.Success(
                        null,
                        "If the account exists, a password reset message has been sent.",
                        200));
                }
                var userResult = await _userService.GetUserByEmailAsync(dto.Email, cancellationToken);
                var user = userResult.Data;
                if (user is null)
                {
                    return Ok(ApiResponse<object?>.Success(
                        null,
                        "If the account exists, a password reset message has been sent.",
                        200));
                }

                await _accountEmailSender.SendPasswordResetLinkAsync(
                    user.Email,
                    user.DisplayName,
                    BuildPasswordResetLink(user.Email, resetToken),
                    cancellationToken);

                _logger.LogInformation("✓ Password reset email sent to: {Email}", dto.Email);
                return Ok(ApiResponse<object?>.Success(
                    null,
                    "If the account exists, a password reset message has been sent.",
                    200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Forgot password error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }


        /// <summary>
        /// Resets a user's password by consuming a valid single-use reset token.
        /// </summary>
        /// <param name="request">Email address, reset type, raw reset token, and new password.</param>
        /// <returns>An empty successful response after the password is reset.</returns>
        /// <response code="200">The password was reset successfully.</response>
        /// <response code="400">The request is invalid, the reset type is unsupported, or the token is invalid or expired.</response>
        /// <response code="500">An unexpected server error occurred.</response>
        /// <remarks>This anonymous endpoint removes the current browser's refresh-token cookie after success.</remarks>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken = default)
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
                    request.NewPassword,
                    cancellationToken);

                if (!success)
                {
                    return BadRequest(ApiResponse<object>.Error(400, "Invalid token or email", null));
                }

                ClearRefreshTokenCookie();
                return Ok(ApiResponse<object?>.Success(null, "Password reset successful", 200));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "❌ Reset password error");
                return StatusCode(500, ApiResponse<object>.Error(500, "Internal server error", null));
            }
        }
    }
}
