using API.Controllers;
using Application.DTOs.Auth;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Enums.Auth;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Responses;
using Shared.Settings;
using UnitTests.TestHelpers;

namespace UnitTests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IAccountEmailSender> _accountEmailSenderMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILogger<AuthController>> _loggerMock = new();
    private readonly IOptions<JwtSettings> _jwtOptions = UnitTestHelper.CreateJwtSettingsOptions();
    private readonly IOptions<EmailConfirmationSettings> _emailConfirmationOptions = UnitTestHelper.CreateEmailConfirmationSettingsOptions();
    private readonly IOptions<EmailTwoFactorSettings> _emailTwoFactorOptions = UnitTestHelper.CreateEmailTwoFactorSettingsOptions();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(
            _jwtTokenServiceMock.Object,
            _authServiceMock.Object,
            _accountEmailSenderMock.Object,
            _userServiceMock.Object,
            _loggerMock.Object,
            _jwtOptions,
            _emailConfirmationOptions,
            _emailTwoFactorOptions);
    }

    [Fact]
    public async Task Login_Returns_ok_when_credentials_are_valid()
    {
        var dto = new LoginUserDto { Email = "test@example.com", Password = "password123" };
        var user = new User { Id = Guid.NewGuid(), Email = EmailAddress.Create(dto.Email), DisplayName = "Test", Role = UserRole.User, IsEmailConfirmed = true, IsTwoFactorEnabled = false };
        var tokens = new JwtTokens { AccessToken = "access-token", RefreshToken = "refresh-token", ExpiresIn = 900 };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _authServiceMock.Setup(x => x.AuthenticateAsync(dto.Email, dto.Password, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(tokens);

        var actionResult = await _controller.Login(dto);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<AuthTokenResponse>>(okResult.Value);
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Login successful", response.Message);
        Assert.Equal(tokens.AccessToken, response.Data?.AccessToken);
        Assert.Contains("drs.refreshToken=refresh-token", setCookieHeader);
        Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_Returns_unauthorized_when_authentication_fails()
    {
        var dto = new LoginUserDto { Email = "bad@example.com", Password = "wrong" };
        _authServiceMock.Setup(x => x.AuthenticateAsync(dto.Email, dto.Password, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var actionResult = await _controller.Login(dto);

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
    }

    [Fact]
    public async Task Login_Returns_forbidden_when_email_is_not_confirmed()
    {
        var dto = new LoginUserDto { Email = "pending@example.com", Password = "password123" };
        var user = new User { Id = Guid.NewGuid(), Email = EmailAddress.Create(dto.Email), DisplayName = "Pending", Role = UserRole.User, IsEmailConfirmed = false, IsTwoFactorEnabled = false };

        _authServiceMock.Setup(x => x.AuthenticateAsync(dto.Email, dto.Password, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var actionResult = await _controller.Login(dto);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);

        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("Email address is not confirmed", response.Message);
    }

    [Fact]
    public async Task Login_Returns_accepted_when_two_factor_is_required()
    {
        var dto = new LoginUserDto { Email = "2fa@example.com", Password = "password123" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = EmailAddress.Create(dto.Email),
            DisplayName = "Two Factor",
            Role = UserRole.User,
            IsEmailConfirmed = true,
            IsTwoFactorEnabled = true
        };
        var challengeId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        _authServiceMock.Setup(x => x.AuthenticateAsync(dto.Email, dto.Password, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _authServiceMock.Setup(x => x.CreateEmailTwoFactorChallengeAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(
            new EmailTwoFactorChallengeDelivery(challengeId, user.Id, user.Email.Value, user.DisplayName, "123456", expiresAt));
        _accountEmailSenderMock.Setup(x => x.SendTwoFactorCodeAsync(user.Email.Value, user.DisplayName, "123456", expiresAt, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var actionResult = await _controller.Login(dto);

        var acceptedResult = Assert.IsType<AcceptedResult>(actionResult);
        var response = Assert.IsType<ApiResponse<TwoFactorChallengeResponseDto>>(acceptedResult.Value);

        Assert.Equal(202, acceptedResult.StatusCode);
        Assert.Equal("Two-factor verification required. Check your email for the code.", response.Message);
        Assert.True(response.Data?.RequiresTwoFactor);
        Assert.Equal(challengeId, response.Data?.ChallengeId);
        _accountEmailSenderMock.Verify(x => x.SendTwoFactorCodeAsync(user.Email.Value, user.DisplayName, "123456", expiresAt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_Returns_created_when_registration_succeeds()
    {
        var dto = new RegisterUserDto { Email = "new@example.com", Password = "password123", FirstName = "New", LastName = "User", CreatedAt = DateTime.UtcNow };
        var user = new User { Id = Guid.NewGuid(), Email = EmailAddress.Create(dto.Email), DisplayName = "New User", Role = UserRole.User, IsEmailConfirmed = false };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _authServiceMock.Setup(x => x.UserExistsAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _authServiceMock.Setup(x => x.RegisterAsync(dto.Email, dto.Password, "New User", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _authServiceMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync("confirmation-token-123456");
        _accountEmailSenderMock.Setup(x => x.SendEmailConfirmationAsync(user.Email.Value, user.DisplayName, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var actionResult = await _controller.Register(dto);

        var createdResult = Assert.IsType<CreatedResult>(actionResult);
        var response = Assert.IsType<ApiResponse<RegisterUserResultDto>>(createdResult.Value);

        Assert.Equal(201, createdResult.StatusCode);
        Assert.Equal("Registration successful. Check your email to confirm the account.", response.Message);
        Assert.Equal(dto.Email, response.Data?.Email);
        Assert.True(response.Data?.RequiresEmailConfirmation);
        Assert.Empty(_controller.Response.Headers.SetCookie.ToString());
        _accountEmailSenderMock.Verify(
            x => x.SendEmailConfirmationAsync(
                user.Email.Value,
                user.DisplayName,
                It.Is<string>(link => link.Contains("confirmation-token-123456", StringComparison.Ordinal) && link.Contains(user.Id.ToString(), StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Register_Returns_bad_request_when_user_already_exists()
    {
        var dto = new RegisterUserDto { Email = "test@example.com", Password = "password123", FirstName = "New", LastName = "User", CreatedAt = DateTime.UtcNow };

        _authServiceMock.Setup(x => x.UserExistsAsync(dto.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var actionResult = await _controller.Register(dto);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("User with this email already exists", response.Message);
    }

    [Fact]
    public async Task Logout_Returns_bad_request_when_refresh_token_is_missing()
    {
        var userId = Guid.NewGuid().ToString();
        var user = ControllerTestHelper.CreateAuthenticatedUser(userId, "user@test.com");

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHelper.CreateHttpContext(user)
        };

        var actionResult = await _controller.Logout();

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Refresh token is required", response.Message);
    }

    [Fact]
    public async Task VerifyToken_Returns_bad_request_when_token_is_missing()
    {
        var request = new VerifyTokenRequest { Token = string.Empty };

        var actionResult = await _controller.VerifyToken(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Token is required", response.Message);
    }

    [Fact]
    public async Task RefreshToken_Returns_ok_when_refresh_succeeds()
    {
        var tokens = new JwtTokens { AccessToken = "access-token", RefreshToken = "new-refresh-token", ExpiresIn = 900 };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = "drs.refreshToken=refresh-token";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync("refresh-token", It.IsAny<CancellationToken>())).ReturnsAsync(tokens);

        var actionResult = await _controller.RefreshToken();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<AuthTokenResponse>>(okResult.Value);
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(tokens.AccessToken, response.Data?.AccessToken);
        Assert.Contains("drs.refreshToken=new-refresh-token", setCookieHeader);
        Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshToken_Returns_unauthorized_when_refresh_token_is_invalid()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = "drs.refreshToken=invalid-refresh";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _jwtTokenServiceMock.Setup(x => x.RefreshTokensAsync("invalid-refresh", It.IsAny<CancellationToken>())).ReturnsAsync((JwtTokens?)null);

        var actionResult = await _controller.RefreshToken();
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Contains("drs.refreshToken=", setCookieHeader);
        Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_Returns_ok_when_refresh_token_is_revoked()
    {
        var userId = Guid.NewGuid().ToString();
        var user = ControllerTestHelper.CreateAuthenticatedUser(userId, "user@test.com");
        var httpContext = ControllerTestHelper.CreateHttpContext(user);
        httpContext.Request.Headers.Cookie = "drs.refreshToken=refresh-token";

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _jwtTokenServiceMock.Setup(x => x.RevokeTokenAsync("refresh-token", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var actionResult = await _controller.Logout();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Logout successful", response.Message);
        Assert.Contains("drs.refreshToken=", setCookieHeader);
        Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutAll_Returns_ok_and_revokes_all_user_sessions()
    {
        var userId = Guid.NewGuid();
        var user = ControllerTestHelper.CreateAuthenticatedUser(userId.ToString(), "user@test.com");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHelper.CreateHttpContext(user)
        };

        _jwtTokenServiceMock
            .Setup(x => x.RevokeAllUserTokensAsync(userId, RevocationReason.UserLogout, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var actionResult = await _controller.LogoutAll();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("All sessions logged out successfully", response.Message);
        Assert.Contains("drs.refreshToken=", setCookieHeader);
        Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        _jwtTokenServiceMock.Verify(
            x => x.RevokeAllUserTokensAsync(userId, RevocationReason.UserLogout, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyToken_Returns_ok_when_token_is_valid()
    {
        var request = new VerifyTokenRequest { Token = "valid-token" };

        _jwtTokenServiceMock.Setup(x => x.ValidateTokenAsync(request.Token, It.IsAny<CancellationToken>())).ReturnsAsync(ControllerTestHelper.CreateAuthenticatedUser(Guid.NewGuid().ToString(), "verify@test.com"));

        var actionResult = await _controller.VerifyToken(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Token is valid", response.Message);
    }

    [Fact]
    public async Task ConfirmEmail_Returns_ok_when_confirmation_succeeds()
    {
        var request = new ConfirmEmailRequestDto
        {
            UserId = Guid.NewGuid(),
            Token = "confirmation-token-123456"
        };

        _authServiceMock.Setup(x => x.ConfirmEmailAsync(request.UserId, request.Token, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var actionResult = await _controller.ConfirmEmail(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Email confirmed successfully", response.Message);
    }
    [Fact]
    public async Task VerifyTwoFactor_Returns_ok_when_code_is_valid()
    {
        var challengeId = Guid.NewGuid();
        var request = new VerifyTwoFactorRequestDto
        {
            ChallengeId = challengeId,
            Code = "123456"
        };
        var user = new User { Id = Guid.NewGuid(), Email = EmailAddress.Create("2fa@example.com"), DisplayName = "Two Factor", Role = UserRole.User };
        var tokens = new JwtTokens { AccessToken = "access-token", RefreshToken = "refresh-token", ExpiresIn = 900 };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _authServiceMock.Setup(x => x.VerifyEmailTwoFactorChallengeAsync(challengeId, request.Code, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenServiceMock.Setup(x => x.GenerateTokensAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(tokens);

        var actionResult = await _controller.VerifyTwoFactor(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<AuthTokenResponse>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Two-factor verification successful", response.Message);
        Assert.Equal(tokens.AccessToken, response.Data?.AccessToken);
    }

    [Fact]
    public async Task ResendTwoFactor_Returns_ok_when_challenge_is_active()
    {
        var challengeId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var request = new ResendTwoFactorRequestDto { ChallengeId = challengeId };

        _authServiceMock.Setup(x => x.ResendEmailTwoFactorChallengeAsync(challengeId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new EmailTwoFactorChallengeDelivery(challengeId, Guid.NewGuid(), "2fa@example.com", "Two Factor", "654321", expiresAt));
        _accountEmailSenderMock.Setup(x => x.SendTwoFactorCodeAsync("2fa@example.com", "Two Factor", "654321", expiresAt, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var actionResult = await _controller.ResendTwoFactor(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<TwoFactorChallengeResponseDto>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("A new verification code has been sent.", response.Message);
        Assert.Equal(challengeId, response.Data?.ChallengeId);
        _accountEmailSenderMock.Verify(x => x.SendTwoFactorCodeAsync("2fa@example.com", "Two Factor", "654321", expiresAt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendConfirmation_Returns_ok_with_generic_message()
    {
        var request = new ResendConfirmationEmailRequestDto { Email = "pending@example.com" };
        var userId = Guid.NewGuid();
        var userDto = new UserDto { Id = userId, Email = request.Email, DisplayName = "Pending User" };

        _userServiceMock.Setup(x => x.GetUserByEmailAsync(request.Email, It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<UserDto>.Success(userDto));
        _authServiceMock.Setup(x => x.IsEmailConfirmedAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _authServiceMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync("confirmation-token-123456");
        _accountEmailSenderMock.Setup(x => x.SendEmailConfirmationAsync(userDto.Email, userDto.DisplayName, It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var actionResult = await _controller.ResendConfirmation(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("If the account exists and is not yet confirmed, a confirmation email has been sent.", response.Message);
    }

    [Fact]
    public async Task GetCurrentUser_Returns_ok_when_user_is_authenticated()
    {
        var userId = Guid.NewGuid();
        var user = ControllerTestHelper.CreateAuthenticatedUser(userId.ToString(), "me@example.com");
        var userDto = new UserDto
        {
            Id = userId,
            Email = "me@example.com",
            DisplayName = "Me User",
            FirstName = "Me",
            LastName = "User",
            Role = "User"
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHelper.CreateHttpContext(user)
        };

        _userServiceMock.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<UserDto>.Success(userDto));

        var actionResult = await _controller.GetCurrentUser();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Current user info", response.Message);
    }
    [Fact]
    public async Task ForgotPassword_Returns_ok_with_generic_message_when_user_does_not_exist()
    {
        var request = new ForgotPasswordRequestDto
        {
            Email = "nonexistent@example.com",
            ResetType = ResetType.Link
        };

        _authServiceMock.Setup(x => x.GeneratePasswordResetTokenAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var actionResult = await _controller.ForgotPassword(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("If the account exists, a password reset message has been sent.", response.Message);
        _authServiceMock.Verify(x => x.GeneratePasswordResetTokenAsync(request.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_Returns_ok_when_reset_request_is_started()
    {
        var request = new ForgotPasswordRequestDto
        {
            Email = "known@example.com",
            ResetType = ResetType.Link
        };

        var user = new UserDto
        {
            Email = request.Email,
            DisplayName = "Known User"
        };

        _authServiceMock.Setup(x => x.GeneratePasswordResetTokenAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync("reset-token");
        _userServiceMock.Setup(x => x.GetUserByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<UserDto>.Success(user));

        var actionResult = await _controller.ForgotPassword(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("If the account exists, a password reset message has been sent.", response.Message);
        _authServiceMock.Verify(x => x.GeneratePasswordResetTokenAsync(request.Email, It.IsAny<CancellationToken>()), Times.Once);
        _accountEmailSenderMock.Verify(
            x => x.SendPasswordResetLinkAsync(request.Email, "Known User", It.Is<string>(link => link.Contains("reset-token")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_Returns_bad_request_when_reset_type_is_not_link()
    {
        var request = new ForgotPasswordRequestDto
        {
            Email = "known@example.com",
            ResetType = ResetType.Code
        };

        var actionResult = await _controller.ForgotPassword(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Only link-based password reset is currently supported", response.Message);
        _authServiceMock.Verify(x => x.GeneratePasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_Returns_bad_request_when_token_is_missing()
    {
        var request = new ResetPasswordRequestDto
        {
            Email = "known@example.com",
            ResetType = ResetType.Link,
            Token = string.Empty,
            NewPassword = "NewPassword123!"
        };

        var actionResult = await _controller.ResetPassword(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Reset token is required", response.Message);
        _authServiceMock.Verify(x => x.ResetPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_Returns_ok_when_reset_succeeds()
    {
        var request = new ResetPasswordRequestDto
        {
            Email = "known@example.com",
            ResetType = ResetType.Link,
            Token = "reset-token-123",
            NewPassword = "NewPassword123!"
        };

        _authServiceMock
            .Setup(x => x.ResetPasswordAsync(request.Email, request.Token!, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var actionResult = await _controller.ResetPassword(request);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        var setCookieHeader = _controller.Response.Headers.SetCookie.ToString();

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal("Password reset successful", response.Message);
        Assert.Contains("drs.refreshToken=", setCookieHeader);
        Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        _authServiceMock.Verify(x => x.ResetPasswordAsync(request.Email, request.Token!, request.NewPassword, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_Returns_bad_request_when_reset_fails()
    {
        var request = new ResetPasswordRequestDto
        {
            Email = "known@example.com",
            ResetType = ResetType.Link,
            Token = "invalid-token",
            NewPassword = "NewPassword123!"
        };

        _authServiceMock
            .Setup(x => x.ResetPasswordAsync(request.Email, request.Token!, request.NewPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var actionResult = await _controller.ResetPassword(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Invalid token or email", response.Message);
    }
}
