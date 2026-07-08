using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace UnitTests.TestHelpers;

public static class UnitTestHelper
{
    public static DbContextOptions<ApplicationDbContext> CreateInMemoryDatabaseOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    public static IOptions<JwtSettings> CreateJwtSettingsOptions()
    {
        return Options.Create(new JwtSettings
        {
            Secret = "test-secret-key-1234567890-test-1234567890-extended",
            Issuer = "test-issuer",
            Audience = "test-audience"
        });
    }

    public static IOptions<EmailConfirmationSettings> CreateEmailConfirmationSettingsOptions()
    {
        return Options.Create(new EmailConfirmationSettings
        {
            PublicOrigin = "http://localhost:3000",
            ConfirmationPath = "/confirm-email",
            TokenExpiresInHours = 24
        });
    }

    public static IOptions<EmailTwoFactorSettings> CreateEmailTwoFactorSettingsOptions()
    {
        return Options.Create(new EmailTwoFactorSettings
        {
            Enabled = true,
            EnableForNewUsers = true,
            CodeExpiresInMinutes = 10,
            CodeLength = 6,
            MaxFailedAttempts = 5
        });
    }
}
