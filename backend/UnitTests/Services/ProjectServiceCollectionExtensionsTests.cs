using API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Settings;

namespace UnitTests.Services;

public class ProjectServiceCollectionExtensionsTests
{
    [Fact]
    public void Production_configuration_rejects_known_example_jwt_secret()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "change-this-to-a-long-random-secret-at-least-32-characters"
            },
            Environments.Production);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
    }

    [Fact]
    public void Production_configuration_requires_secure_refresh_cookie_policy()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenCookieSecurePolicy"] = "SameAsRequest"
            },
            Environments.Production);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
    }

    [Fact]
    public void Forwarded_headers_use_only_configured_networks()
    {
        using var provider = BuildProvider(
            new Dictionary<string, string?>
            {
                ["ForwardedHeaders:Enabled"] = "true",
                ["ForwardedHeaders:KnownNetworks:0"] = "10.20.0.0/16"
            },
            Environments.Development);

        var settings = provider.GetRequiredService<IOptions<ForwardedHeadersSettings>>().Value;
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.True(settings.Enabled);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Single(options.KnownNetworks);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    public void Data_protection_persists_keys_in_configured_key_ring()
    {
        var keyRingDirectory = Directory.CreateTempSubdirectory("dotnet-react-data-protection-");

        try
        {
            using var provider = BuildProvider(
                new Dictionary<string, string?>
                {
                    ["DataProtection:KeyRingPath"] = keyRingDirectory.FullName
                },
                Environments.Development);

            var protector = provider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("ProjectServiceCollectionExtensionsTests");

            var protectedValue = protector.Protect("test-value");

            Assert.NotEqual("test-value", protectedValue);
            Assert.NotEmpty(Directory.EnumerateFiles(keyRingDirectory.FullName));
        }
        finally
        {
            keyRingDirectory.Delete(true);
        }
    }

    private static ServiceProvider BuildProvider(
        IReadOnlyDictionary<string, string?> overrides,
        string environmentName)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
            ["Jwt:Secret"] = "test-secret-key-1234567890-test-1234567890-extended",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:AccessTokenExpiresInMinutes"] = "15",
            ["Jwt:RefreshTokenExpiresInDays"] = "7",
            ["Jwt:RefreshTokenCookieName"] = "drs.refreshToken",
            ["Jwt:RefreshTokenCookiePath"] = "/api/auth",
            ["Jwt:RefreshTokenCookieSameSite"] = "Lax",
            ["Jwt:RefreshTokenCookieSecurePolicy"] = "Always",
            ["Jwt:RefreshTokenCookieIsEssential"] = "true",
            ["Cors:AllowCredentials"] = "true",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["DataProtection:ApplicationName"] = "UnitTests",
            ["DataProtection:KeyRingPath"] = Path.Combine(Path.GetTempPath(), "dotnet-react-unit-test-keys"),
            ["ForwardedHeaders:Enabled"] = "false",
            ["ForwardedHeaders:KnownNetworks:0"] = "172.28.0.0/16",
            ["ForwardedHeaders:ForwardLimit"] = "1",
            ["EmailConfirmation:PublicOrigin"] = "http://localhost:3000",
            ["EmailConfirmation:ConfirmationPath"] = "/confirm-email",
            ["EmailConfirmation:TokenExpiresInHours"] = "24",
            ["EmailTwoFactor:CodeExpiresInMinutes"] = "10",
            ["EmailTwoFactor:CodeLength"] = "6",
            ["EmailTwoFactor:MaxFailedAttempts"] = "5",
            ["AuthSecurity:RateLimitPermitLimit"] = "5",
            ["AuthSecurity:RateLimitWindowSeconds"] = "60",
            ["AuthSecurity:MaxFailedLoginAttempts"] = "5",
            ["AuthSecurity:LockoutDurationMinutes"] = "15"
        };

        foreach (var overrideSetting in overrides)
        {
            settings[overrideSetting.Key] = overrideSetting.Value;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);

        var services = new ServiceCollection();
        services.AddProjectServices(configuration, environment.Object);

        return services.BuildServiceProvider();
    }
}