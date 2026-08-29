using Domain.Entities;
using Application.DTOs.Auth;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Enums.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using Shared.Responses;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using API.Controllers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace IntegrationTests;

public class AuthApiIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthApiIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Login_Returns_access_token_and_sets_refresh_cookie_for_valid_credentials()
    {
        await SeedUserAsync("test@example.com", "password123", "Test User", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "test@example.com", Password = "password123" });

        loginResponse.EnsureSuccessStatusCode();

        var apiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(apiResponse?.Data);
        Assert.False(string.IsNullOrWhiteSpace(apiResponse.Data.AccessToken));
        Assert.True(apiResponse.Data.ExpiresIn > 0);
        var setCookieHeader = loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? string.Join(";", cookies)
            : string.Empty;
        Assert.Contains("drs.refreshToken=", setCookieHeader);
        Assert.Contains("HttpOnly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path=/api/auth", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", setCookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_Returns_current_user_when_authorized()
    {
        await SeedUserAsync("test@example.com", "password123", "Test User", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "test@example.com", Password = "password123" });
        loginResponse.EnsureSuccessStatusCode();

        var loginApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(loginApiResponse?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginApiResponse.Data.AccessToken);
        var meResponse = await _client.GetAsync("/api/auth/me");

        meResponse.EnsureSuccessStatusCode();
        var meApiResponse = await meResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(meApiResponse?.Data);
        Assert.Equal("Current user info", meApiResponse.Message);
    }

    [Fact]
    public async Task RefreshToken_Returns_new_tokens_when_refresh_token_is_valid()
    {
        await SeedUserAsync("test@example.com", "password123", "Test User", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "test@example.com", Password = "password123" });
        loginResponse.EnsureSuccessStatusCode();

        var loginApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(loginApiResponse?.Data);

        var refreshResponse = await _client.PostAsync("/api/auth/refresh-token", null);
        refreshResponse.EnsureSuccessStatusCode();

        var refreshApiResponse = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(refreshApiResponse?.Data);
        Assert.NotEqual(loginApiResponse.Data.AccessToken, refreshApiResponse.Data.AccessToken);
    }

    [Fact]
    public async Task Me_Returns_unauthorized_when_token_is_missing()
    {
        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task Me_Returns_unauthorized_when_token_is_invalid()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyToken_Returns_unauthorized_when_token_is_invalid()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/verify-token", new
        {
            Token = "invalid-token"
        });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Returns_unauthorized_when_refresh_token_is_invalid()
    {
        using var invalidCookieClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        invalidCookieClient.DefaultRequestHeaders.Add("Cookie", "drs.refreshToken=invalid-refresh-token");

        var response = await invalidCookieClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Cannot_reuse_old_refresh_token_after_rotation()
    {
        await SeedUserAsync("test@example.com", "password123", "Test User", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "test@example.com",
            Password = "password123"
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(loginApiResponse?.Data);

        var initialCookie = GetRefreshTokenCookie(loginResponse);

        var firstRefreshResponse = await _client.PostAsync("/api/auth/refresh-token", null);

        firstRefreshResponse.EnsureSuccessStatusCode();

        var rotatedCookie = GetRefreshTokenCookie(firstRefreshResponse);

        using var replayClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        replayClient.DefaultRequestHeaders.Add("Cookie", initialCookie);

        var secondRefreshResponse = await replayClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, secondRefreshResponse.StatusCode);
        Assert.NotEqual(initialCookie, rotatedCookie);

        using var successorClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        successorClient.DefaultRequestHeaders.Add("Cookie", rotatedCookie);

        var successorRefreshResponse = await successorClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, successorRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Returns_unauthorized_when_user_is_deactivated_after_login()
    {
        const string email = "inactive.refresh@example.com";
        await SeedUserAsync(email, "password123", "Inactive Refresh", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        loginResponse.EnsureSuccessStatusCode();
        var initialCookie = GetRefreshTokenCookie(loginResponse);

        await UpdateUserAsync(email, user => user.IsActive = false);

        using var inactiveClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        inactiveClient.DefaultRequestHeaders.Add("Cookie", initialCookie);

        var refreshResponse = await inactiveClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Uses_current_user_role_after_role_change()
    {
        const string email = "role.refresh@example.com";
        await SeedUserAsync(email, "password123", "Role Refresh", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        loginResponse.EnsureSuccessStatusCode();

        await UpdateUserAsync(email, user => user.Role = UserRole.Admin);

        var refreshResponse = await _client.PostAsync("/api/auth/refresh-token", null);
        refreshResponse.EnsureSuccessStatusCode();
        var refreshApiResponse = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(refreshApiResponse?.Data);

        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(refreshApiResponse.Data.AccessToken);
        Assert.Contains(accessToken.Claims, claim =>
            (claim.Type == "role" || claim.Type == ClaimTypes.Role)
            && claim.Value == UserRole.Admin.ToString());
    }

    [Fact]
    public async Task ChangePassword_Rejects_the_previous_refresh_session()
    {
        const string email = "change.session@example.com";
        await SeedUserAsync(email, "password123", "Change Session", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        loginResponse.EnsureSuccessStatusCode();
        var oldCookie = GetRefreshTokenCookie(loginResponse);
        var loginApiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(loginApiResponse?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginApiResponse.Data.AccessToken);
        var changeResponse = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            CurrentPassword = "password123",
            NewPassword = "newPassword123"
        });
        changeResponse.EnsureSuccessStatusCode();

        using var oldSessionClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        oldSessionClient.DefaultRequestHeaders.Add("Cookie", oldCookie);

        var refreshResponse = await oldSessionClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task PasswordReset_Rejects_the_previous_refresh_session()
    {
        const string email = "reset.session@example.com";
        _factory.EmailSender.Clear();
        await SeedUserAsync(email, "password123", "Reset Session", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        loginResponse.EnsureSuccessStatusCode();
        var oldCookie = GetRefreshTokenCookie(loginResponse);

        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            Email = email,
            ResetType = ResetType.Link
        });
        forgotResponse.EnsureSuccessStatusCode();

        var resetLink = new Uri(_factory.EmailSender.LatestPasswordResetLink!);
        var parameters = resetLink.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            Email = email,
            ResetType = ResetType.Link,
            Token = parameters["token"],
            NewPassword = "newPassword123"
        });
        resetResponse.EnsureSuccessStatusCode();

        using var oldSessionClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        oldSessionClient.DefaultRequestHeaders.Add("Cookie", oldCookie);

        var refreshResponse = await oldSessionClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Allows_only_one_successor_for_concurrent_requests()
    {
        const string email = "concurrent.api.refresh@example.com";
        await SeedUserAsync(email, "password123", "Concurrent API Refresh", UserRole.User);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        loginResponse.EnsureSuccessStatusCode();
        var initialCookie = GetRefreshTokenCookie(loginResponse);

        using var firstClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        using var secondClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        firstClient.DefaultRequestHeaders.Add("Cookie", initialCookie);
        secondClient.DefaultRequestHeaders.Add("Cookie", initialCookie);

        var responses = await Task.WhenAll(
            firstClient.PostAsync("/api/auth/refresh-token", null),
            secondClient.PostAsync("/api/auth/refresh-token", null));

        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == System.Net.HttpStatusCode.Unauthorized);

        var successfulResponse = responses.Single(response => response.IsSuccessStatusCode);
        var successorCookie = GetRefreshTokenCookie(successfulResponse);
        using var successorClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        successorClient.DefaultRequestHeaders.Add("Cookie", successorCookie);

        var successorRefreshResponse = await successorClient.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, successorRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_Rejects_refresh_sessions_from_all_devices()
    {
        const string email = "logout.all@example.com";
        await SeedUserAsync(email, "password123", "Logout All", UserRole.User);

        var firstLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        firstLoginResponse.EnsureSuccessStatusCode();
        var firstCookie = GetRefreshTokenCookie(firstLoginResponse);

        var secondLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "password123"
        });
        secondLoginResponse.EnsureSuccessStatusCode();
        var secondCookie = GetRefreshTokenCookie(secondLoginResponse);
        var secondLoginApiResponse = await secondLoginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(secondLoginApiResponse?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondLoginApiResponse.Data.AccessToken);
        var logoutAllResponse = await _client.PostAsync("/api/auth/logout-all", null);
        logoutAllResponse.EnsureSuccessStatusCode();

        using var firstSessionClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        using var secondSessionClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        firstSessionClient.DefaultRequestHeaders.Add("Cookie", firstCookie);
        secondSessionClient.DefaultRequestHeaders.Add("Cookie", secondCookie);

        var refreshResponses = await Task.WhenAll(
            firstSessionClient.PostAsync("/api/auth/refresh-token", null),
            secondSessionClient.PostAsync("/api/auth/refresh-token", null));

        Assert.All(refreshResponses, response => Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode));
    }

    [Fact]
    public async Task Register_Creates_user_that_can_login_later()
    {
        _factory.EmailSender.Clear();

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FirstName = "New",
            LastName = "User",
            Email = "new.user@example.com",
            Password = "password123",
            PhoneNumber = "123456789",
            Address = "Main Street",
            CreatedAt = DateTime.UtcNow
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, registerResponse.StatusCode);

        var registerApiResponse = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegisterUserResultDto>>();
        Assert.NotNull(registerApiResponse?.Data);
        Assert.Equal("new.user@example.com", registerApiResponse.Data.Email);
        Assert.True(registerApiResponse.Data.RequiresEmailConfirmation);
        Assert.Equal("Registration successful. Check your email to confirm the account.", registerApiResponse.Message);
        Assert.NotNull(_factory.EmailSender.LatestConfirmationLink);

        _client.DefaultRequestHeaders.Authorization = null;

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "new.user@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_Enables_login_for_a_new_registration()
    {
        _factory.EmailSender.Clear();

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FirstName = "Email",
            LastName = "Pending",
            Email = "confirm.me@example.com",
            Password = "password123",
            PhoneNumber = "123456789",
            Address = "Main Street",
            CreatedAt = DateTime.UtcNow
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, registerResponse.StatusCode);

        var confirmationPayload = GetLatestConfirmationPayload();
        var confirmResponse = await _client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            confirmationPayload.UserId,
            confirmationPayload.Token
        });

        confirmResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "confirm.me@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Accepted, loginResponse.StatusCode);

        var challengeResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TwoFactorChallengeResponseDto>>();
        Assert.NotNull(challengeResponse?.Data);

        var verifyTwoFactorResponse = await _client.PostAsJsonAsync("/api/auth/verify-2fa", new
        {
            ChallengeId = challengeResponse.Data.ChallengeId,
            Code = GetLatestTwoFactorCode()
        });

        verifyTwoFactorResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResendConfirmation_Returns_generic_success_and_reissues_link()
    {
        _factory.EmailSender.Clear();

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FirstName = "Resend",
            LastName = "Case",
            Email = "resend.confirm@example.com",
            Password = "password123",
            PhoneNumber = "123456789",
            Address = "Main Street",
            CreatedAt = DateTime.UtcNow
        });

        Assert.Equal(System.Net.HttpStatusCode.Created, registerResponse.StatusCode);
        var firstPayload = GetLatestConfirmationPayload();

        var resendResponse = await _client.PostAsJsonAsync("/api/auth/resend-confirmation", new
        {
            Email = "resend.confirm@example.com"
        });

        resendResponse.EnsureSuccessStatusCode();

        var resendApiResponse = await resendResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(resendApiResponse);
        Assert.Equal("If the account exists and is not yet confirmed, a confirmation email has been sent.", resendApiResponse.Message);

        var secondPayload = GetLatestConfirmationPayload();
        Assert.NotEqual(firstPayload.Token, secondPayload.Token);
    }

    [Fact]
    public async Task PasswordReset_Sends_link_and_changes_password()
    {
        _factory.EmailSender.Clear();
        await SeedUserAsync("password.reset@example.com", "password123", "Password Reset", UserRole.User);

        var forgotResponse = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            Email = "password.reset@example.com",
            ResetType = ResetType.Link
        });

        forgotResponse.EnsureSuccessStatusCode();
        Assert.NotNull(_factory.EmailSender.LatestPasswordResetLink);

        var resetLink = new Uri(_factory.EmailSender.LatestPasswordResetLink!);
        var parameters = resetLink.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

        Assert.Equal("password.reset@example.com", parameters["email"]);
        Assert.False(string.IsNullOrWhiteSpace(parameters["token"]));

        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            Email = parameters["email"],
            ResetType = ResetType.Link,
            Token = parameters["token"],
            NewPassword = "newPassword123"
        });

        resetResponse.EnsureSuccessStatusCode();

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "password.reset@example.com",
            Password = "password123"
        });
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "password.reset@example.com",
            Password = "newPassword123"
        });
        newLoginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Login_Returns_two_factor_challenge_when_email_2fa_is_enabled()
    {
        _factory.EmailSender.Clear();
        await SeedUserAsync("2fa.user@example.com", "password123", "Two Factor User", UserRole.User, isTwoFactorEnabled: true);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "2fa.user@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Accepted, loginResponse.StatusCode);

        var apiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TwoFactorChallengeResponseDto>>();
        Assert.NotNull(apiResponse?.Data);
        Assert.True(apiResponse.Data.RequiresTwoFactor);
        Assert.False(string.IsNullOrWhiteSpace(_factory.EmailSender.LatestTwoFactorCode));
    }

    [Fact]
    public async Task Authenticator_setup_confirm_and_login_flow_uses_totp_challenge()
    {
        const string email = "authenticator.user@example.com";
        await SeedUserAsync(email, "password123", "Authenticator User", UserRole.User);

        var tokens = await LoginAsync(email, "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var setupResponse = await _client.PostAsync("/api/auth/authenticator/setup", null);
        setupResponse.EnsureSuccessStatusCode();
        var setup = await setupResponse.Content.ReadFromJsonAsync<ApiResponse<AuthenticatorSetupDto>>();
        Assert.False(string.IsNullOrWhiteSpace(setup?.Data?.SharedKey));
        Assert.StartsWith("otpauth://totp/", setup.Data.ProvisioningUri);

        var authenticatorCode = new Totp(Base32Encoding.ToBytes(setup.Data.SharedKey)).ComputeTotp();
        var confirmResponse = await _client.PostAsJsonAsync("/api/auth/authenticator/confirm", new { Code = authenticatorCode });
        confirmResponse.EnsureSuccessStatusCode();
        var confirmation = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<AuthenticatorConfirmationDto>>();
        Assert.Equal(10, confirmation?.Data?.RecoveryCodes.Count);

        _client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "password123" });
        Assert.Equal(System.Net.HttpStatusCode.Accepted, loginResponse.StatusCode);
        var challenge = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TwoFactorChallengeResponseDto>>();
        Assert.Equal("authenticator", challenge?.Data?.Method);

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-2fa", new
        {
            ChallengeId = challenge!.Data!.ChallengeId,
            Code = authenticatorCode
        });
        verifyResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ResendTwoFactor_Rotates_code_for_active_challenge()
    {
        _factory.EmailSender.Clear();
        await SeedUserAsync("2fa.resend@example.com", "password123", "Two Factor Resend", UserRole.User, isTwoFactorEnabled: true);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "2fa.resend@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Accepted, loginResponse.StatusCode);

        var challengeResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<TwoFactorChallengeResponseDto>>();
        Assert.NotNull(challengeResponse?.Data);

        var firstCode = GetLatestTwoFactorCode();

        var resendResponse = await _client.PostAsJsonAsync("/api/auth/resend-2fa", new
        {
            ChallengeId = challengeResponse.Data.ChallengeId
        });

        resendResponse.EnsureSuccessStatusCode();

        var secondCode = GetLatestTwoFactorCode();
        Assert.NotEqual(firstCode, secondCode);
    }

    [Fact]
    public async Task ChangePassword_Updates_credentials_for_the_authenticated_user()
    {
        await SeedUserAsync("password.change@example.com", "password123", "Password Change", UserRole.User);

        var tokens = await LoginAsync("password.change@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var changeResponse = await _client.PostAsJsonAsync("/api/auth/change-password", new
        {
            CurrentPassword = "password123",
            NewPassword = "newPassword123"
        });

        changeResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;

        var oldLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "password.change@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        var newLoginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "password.change@example.com",
            Password = "newPassword123"
        });

        newLoginResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Admin_endpoint_returns_forbidden_for_user_role()
    {
        await SeedUserAsync("user@example.com", "password123", "Normal User", UserRole.User);

        var tokens = await LoginAsync("user@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.GetAsync("/api/users/count");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoint_allows_admin_role()
    {
        await SeedUserAsync("admin@example.com", "password123", "Admin User", UserRole.Admin);

        var tokens = await LoginAsync("admin@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.GetAsync("/api/users/count");

        Assert.NotEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Returns_bad_request_after_logout_clears_refresh_cookie()
    {
        await SeedUserAsync("logout@example.com", "password123", "Logout User", UserRole.User);

        var tokens = await LoginAsync("logout@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);

        logoutResponse.EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Authorization = null;

        var refreshResponse = await _client.PostAsync("/api/auth/refresh-token", null);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_Returns_unauthorized_when_access_token_is_missing()
    {
        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        await SeedUserAsync("ratelimit@example.com", "password123", "Rate Limit User", UserRole.User);

        System.Net.HttpStatusCode? lastStatusCode = null;

        for (int i = 0; i < 6; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/login", new { Email = "ratelimit@example.com", Password = "invalid-password" });
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    [Fact]
    public async Task Login_Returns_unauthorized_when_account_is_locked_after_failed_attempts()
    {
        await SeedUserAsync("locked.login@example.com", "password123", "Locked Login User", UserRole.User);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var failedResponse = await _client.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "locked.login@example.com",
                Password = "invalid-password"
            });

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        var lockedResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "locked.login@example.com",
            Password = "password123"
        });

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, lockedResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == "locked.login@example.com");
        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.True(user.LockoutEndAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ForgotPassword_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        System.Net.HttpStatusCode? lastStatusCode = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new
            {
                Email = "unknown-reset@example.com",
                ResetType = ResetType.Link
            });
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    [Fact]
    public async Task RefreshToken_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        System.Net.HttpStatusCode? lastStatusCode = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await _client.PostAsync("/api/auth/refresh-token", null);
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    [Fact]
    public async Task VerifyToken_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        System.Net.HttpStatusCode? lastStatusCode = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/verify-token", new
            {
                Token = "invalid-token"
            });
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    [Fact]
    public async Task ChangePassword_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        await SeedUserAsync("change-password-limit@example.com", "password123", "Change Password Limit", UserRole.User);
        var tokens = await LoginAsync("change-password-limit@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        System.Net.HttpStatusCode? lastStatusCode = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/change-password", new
            {
                CurrentPassword = "invalid-password",
                NewPassword = "new-password"
            });
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    [Fact]
    public async Task AuthenticatorManagement_Returns_TooManyRequests_When_RateLimit_Exceeded()
    {
        await SeedUserAsync("authenticator-limit@example.com", "password123", "Authenticator Limit", UserRole.User);
        var tokens = await LoginAsync("authenticator-limit@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        System.Net.HttpStatusCode? lastStatusCode = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/authenticator/confirm", new
            {
                Code = "000000"
            });
            lastStatusCode = response.StatusCode;
        }

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, lastStatusCode);
    }

    private async Task<AuthTokenResponse> LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        var apiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(apiResponse?.Data);
        return apiResponse.Data;
    }

    private static string GetRefreshTokenCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var refreshTokenCookie = cookies.FirstOrDefault(value => value.Contains("drs.refreshToken="));
        Assert.False(string.IsNullOrWhiteSpace(refreshTokenCookie));
        return refreshTokenCookie!;
    }

    private async Task SeedUserAsync(string email, string password, string displayName, UserRole role, bool isTwoFactorEnabled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            IsEmailConfirmed = true,
            IsTwoFactorEnabled = isTwoFactorEnabled,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private async Task UpdateUserAsync(string email, Action<User> update)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Email == email);

        update(user);
        await dbContext.SaveChangesAsync();
    }

    private ConfirmationPayload GetLatestConfirmationPayload()
    {
        var confirmationLink = _factory.EmailSender.LatestConfirmationLink;
        Assert.False(string.IsNullOrWhiteSpace(confirmationLink));

        var query = new Uri(confirmationLink!).Query.TrimStart('?');
        var parameters = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                parts => parts[0],
                parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

        Assert.True(parameters.TryGetValue("userId", out var userIdText));
        Assert.True(Guid.TryParse(userIdText, out var userId));
        Assert.True(parameters.TryGetValue("token", out var token));
        Assert.False(string.IsNullOrWhiteSpace(token));

        return new ConfirmationPayload(userId, token!);
    }

    private string GetLatestTwoFactorCode()
    {
        var code = _factory.EmailSender.LatestTwoFactorCode;
        Assert.False(string.IsNullOrWhiteSpace(code));
        return code!;
    }

    private sealed record ConfirmationPayload(Guid UserId, string Token);

}
