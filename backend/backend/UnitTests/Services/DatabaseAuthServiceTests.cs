using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Enums;
using Domain.Enums.Auth;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace UnitTests.Services;

public class DatabaseAuthServiceTests
{
    private static IOptions<EmailConfirmationSettings> CreateEmailConfirmationOptions()
        => Options.Create(new EmailConfirmationSettings
        {
            PublicOrigin = "https://example.com",
            ConfirmationPath = "/confirm-email",
            TokenExpiresInHours = 24
        });

    private static IOptions<EmailTwoFactorSettings> CreateEmailTwoFactorOptions()
        => Options.Create(new EmailTwoFactorSettings
        {
            Enabled = true,
            EnableForNewUsers = true,
            CodeLength = 6,
            CodeExpiresInMinutes = 10,
            MaxFailedAttempts = 3
        });

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static DatabaseAuthService CreateService(ApplicationDbContext dbContext)
        => new(
            dbContext,
            CreateEmailConfirmationOptions(),
            CreateEmailTwoFactorOptions(),
            Mock.Of<ILogger<DatabaseAuthService>>());

    [Fact]
    public async Task SendPasswordResetEmailAsync_Returns_false_when_user_does_not_exist()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var result = await service.SendPasswordResetEmailAsync("missing@example.com");

        Assert.False(result);
        Assert.Empty(dbContext.PasswordResetRequests);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_Creates_reset_request_when_user_exists()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "known@example.com",
            DisplayName = "Known User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.SendPasswordResetEmailAsync("known@example.com");

        Assert.True(result);
        var request = await dbContext.PasswordResetRequests.SingleAsync();
        Assert.Equal(ResetType.Link, request.ResetType);
        Assert.Equal(string.Empty, request.CodeHash);
        Assert.False(string.IsNullOrWhiteSpace(request.TokenHash));
        Assert.Null(request.ConsumedAt);
        Assert.Null(request.RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_Returns_false_when_token_is_invalid()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "known@example.com",
            DisplayName = "Known User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = "old-password-hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ResetPasswordAsync("known@example.com", "invalid-token", "NewPassword123!");

        Assert.False(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_Returns_false_when_reset_request_is_expired()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "known@example.com",
            DisplayName = "Known User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = "old-password-hash"
        };
        dbContext.Users.Add(user);
        dbContext.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ResetType = ResetType.Link,
            TokenHash = HashTokenForTest("expired-token"),
            CodeHash = string.Empty,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastSentAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            FailedAttempts = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ResetPasswordAsync("known@example.com", "expired-token", "NewPassword123!");

        Assert.False(result);
        var request = await dbContext.PasswordResetRequests.SingleAsync();
        Assert.NotNull(request.RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_Returns_true_when_token_is_valid()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "known@example.com",
            DisplayName = "Known User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = "old-password-hash"
        };
        dbContext.Users.Add(user);
        dbContext.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ResetType = ResetType.Link,
            TokenHash = HashTokenForTest("valid-token"),
            CodeHash = string.Empty,
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            FailedAttempts = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ResetPasswordAsync("known@example.com", "valid-token", "NewPassword123!");

        Assert.True(result);
        var updatedUser = await dbContext.Users.SingleAsync();
        Assert.NotEqual("old-password-hash", updatedUser.PasswordHash);
        var updatedRequest = await dbContext.PasswordResetRequests.SingleAsync();
        Assert.NotNull(updatedRequest.ConsumedAt);
    }

    [Fact]
    public async Task CreateEmailTwoFactorChallengeAsync_Creates_challenge_for_eligible_user()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "2fa@example.com",
            DisplayName = "Two Factor",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            IsTwoFactorEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.CreateEmailTwoFactorChallengeAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.UserId);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        Assert.Single(dbContext.EmailTwoFactorChallenges);
    }

    [Fact]
    public async Task VerifyEmailTwoFactorChallengeAsync_Returns_user_when_code_is_valid()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "2fa@example.com",
            DisplayName = "Two Factor",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            IsTwoFactorEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        dbContext.EmailTwoFactorChallenges.Add(new EmailTwoFactorChallenge
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = HashTokenForTest("123456"),
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            FailedAttempts = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.VerifyEmailTwoFactorChallengeAsync(dbContext.EmailTwoFactorChallenges.Single().Id, "123456");

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        var updatedChallenge = await dbContext.EmailTwoFactorChallenges.SingleAsync();
        Assert.NotNull(updatedChallenge.ConsumedAt);
    }

    private static string HashTokenForTest(string token)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }
}
