using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace UnitTests.Services;

public class DatabaseAuthServiceLockoutTests
{
    [Fact]
    public async Task AuthenticateAsync_Locks_account_after_configured_failed_attempts()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("locked@example.com", "correct-password");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, maxFailedLoginAttempts: 3);

        Assert.Null(await service.AuthenticateAsync(user.Email.Value, "wrong-password"));
        Assert.Null(await service.AuthenticateAsync(user.Email.Value, "wrong-password"));
        Assert.Null(await service.AuthenticateAsync(user.Email.Value, "wrong-password"));
        Assert.Null(await service.AuthenticateAsync(user.Email.Value, "correct-password"));

        var lockedUser = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal(3, lockedUser.FailedLoginAttempts);
        Assert.True(lockedUser.LockoutEndAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthenticateAsync_Resets_failed_attempts_after_successful_login()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("reset@example.com", "correct-password");
        user.RecordFailedLogin(DateTime.UtcNow, 3, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(DateTime.UtcNow, 3, TimeSpan.FromMinutes(15));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, maxFailedLoginAttempts: 3);

        var authenticatedUser = await service.AuthenticateAsync(user.Email.Value, "correct-password");

        Assert.NotNull(authenticatedUser);
        var updatedUser = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal(0, updatedUser.FailedLoginAttempts);
        Assert.Null(updatedUser.LockoutEndAt);
    }

    [Fact]
    public async Task AuthenticateAsync_Allows_login_after_lockout_expiry()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser("expired@example.com", "correct-password");
        var expiredAt = DateTime.UtcNow.AddMinutes(-16);
        user.RecordFailedLogin(expiredAt, 3, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(expiredAt, 3, TimeSpan.FromMinutes(15));
        user.RecordFailedLogin(expiredAt, 3, TimeSpan.FromMinutes(15));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, maxFailedLoginAttempts: 3);

        var authenticatedUser = await service.AuthenticateAsync(user.Email.Value, "correct-password");

        Assert.NotNull(authenticatedUser);
        var updatedUser = await dbContext.Users.AsNoTracking().SingleAsync();
        Assert.Equal(0, updatedUser.FailedLoginAttempts);
        Assert.Null(updatedUser.LockoutEndAt);
    }

    [Fact]
    public async Task AuthenticateAsync_Returns_null_for_unknown_account_without_persisting_state()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var authenticatedUser = await service.AuthenticateAsync("unknown@example.com", "wrong-password");

        Assert.Null(authenticatedUser);
        Assert.Empty(dbContext.Users);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static DatabaseAuthService CreateService(ApplicationDbContext dbContext, int maxFailedLoginAttempts = 3)
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
            Options.Create(new AuthSecuritySettings
            {
                RateLimitPermitLimit = 5,
                RateLimitWindowSeconds = 60,
                MaxFailedLoginAttempts = maxFailedLoginAttempts,
                LockoutDurationMinutes = 15
            }),
            DataProtectionProvider.Create("DatabaseAuthServiceLockoutTests"),
            NullLogger<DatabaseAuthService>.Instance);

    private static User CreateUser(string email, string password)
    {
        var user = User.Create(
            EmailAddress.Create(email),
            DisplayName.Create("Test User"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true);
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, password));
        return user;
    }
}