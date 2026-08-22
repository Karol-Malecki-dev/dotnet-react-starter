using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Enums.Auth;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace UnitTests.Services;

public class DatabaseAuthServiceTests
{
    [Fact]
    public async Task ChangePasswordAsync_Revokes_active_refresh_sessions()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "old-password");
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(CreateRefreshToken(user));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangePasswordAsync(user.Id, "old-password", "new-password");

        Assert.True(result);
        var refreshToken = await dbContext.RefreshTokens.SingleAsync();
        Assert.NotNull(refreshToken.RevokedAt);
        Assert.Equal(RevocationReason.PasswordChanged, refreshToken.RevocationReason);
    }

    [Fact]
    public async Task ResetPasswordAsync_Revokes_active_refresh_sessions()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        user.PasswordHash = "old-password-hash";
        dbContext.Users.Add(user);
        dbContext.RefreshTokens.Add(CreateRefreshToken(user));
        dbContext.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ResetType = ResetType.Link,
            TokenHash = HashTokenForTest("reset-token"),
            CodeHash = string.Empty,
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            FailedAttempts = 0
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ResetPasswordAsync(user.Email, "reset-token", "new-password");

        Assert.True(result);
        var refreshToken = await dbContext.RefreshTokens.SingleAsync();
        Assert.NotNull(refreshToken.RevokedAt);
        Assert.Equal(RevocationReason.PasswordReset, refreshToken.RevocationReason);
    }

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
            Options.Create(new EmailConfirmationSettings
            {
                PublicOrigin = "https://example.com",
                ConfirmationPath = "/confirm-email",
                TokenExpiresInHours = 24
            }),
            Options.Create(new EmailTwoFactorSettings
            {
                Enabled = true,
                EnableForNewUsers = true,
                CodeLength = 6,
                CodeExpiresInMinutes = 10,
                MaxFailedAttempts = 3
            }),
            DataProtectionProvider.Create("UnitTests"),
            NullLogger<DatabaseAuthService>.Instance);

    private static User CreateUser()
        => new()
        {
            Id = Guid.NewGuid(),
            Email = "sessions@example.com",
            DisplayName = "Sessions User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

    private static RefreshToken CreateRefreshToken(User user)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserEmail = user.Email,
            UserDisplayName = user.DisplayName,
            UserRole = user.Role,
            IsEmailConfirmed = user.IsEmailConfirmed,
            TokenHash = HashTokenForTest("refresh-token"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedByIp = "127.0.0.1",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            FamilyId = Guid.NewGuid()
        };

    private static string HashTokenForTest(string token)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}