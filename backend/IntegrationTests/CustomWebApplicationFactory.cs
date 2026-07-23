using Infrastructure.Data;
using Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"IntegrationTestDb_{Guid.NewGuid()}";

    public TestAccountEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Integration");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        builder.UseSetting("DbConnectionString", "Host=localhost;Port=5432;Database=test;Username=test;Password=test");
        builder.UseSetting("Jwt:Secret", "test-secret-key-1234567890-test-1234567890-extended");
        builder.UseSetting("Jwt:Issuer", "test-issuer");
        builder.UseSetting("Jwt:Audience", "test-audience");
        builder.UseSetting("Jwt:AccessTokenExpiresInMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpiresInDays", "7");
        builder.UseSetting("Jwt:RefreshTokenCookieSecurePolicy", "SameAsRequest");
        builder.UseSetting("EmailConfirmation:PublicOrigin", "http://localhost:3000");
        builder.UseSetting("EmailConfirmation:ConfirmationPath", "/confirm-email");
        builder.UseSetting("EmailConfirmation:TokenExpiresInHours", "24");
        builder.UseSetting("EmailTwoFactor:Enabled", "true");
        builder.UseSetting("EmailTwoFactor:EnableForNewUsers", "true");
        builder.UseSetting("EmailTwoFactor:CodeExpiresInMinutes", "10");
        builder.UseSetting("EmailTwoFactor:CodeLength", "6");
        builder.UseSetting("EmailTwoFactor:MaxFailedAttempts", "5");
        builder.UseSetting("UiFeatures:GlobalSearchEnabled", "true");
        builder.UseSetting("UiFeatures:DashboardOverviewEnabled", "true");
        builder.UseSetting("UiFeatures:AdminNavigationEnabled", "true");
        builder.UseSetting("UiFeatures:UserManagementNavigationEnabled", "true");
        builder.UseSetting("UiFeatures:EmailFeatureSectionsEnabled", "true");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["DbConnectionString"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["DefaultConnection"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Jwt:Secret"] = "test-secret-key-1234567890-test-1234567890-extended",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiresInMinutes"] = "15",
                ["Jwt:RefreshTokenExpiresInDays"] = "7",
                ["Jwt:RefreshTokenCookieSecurePolicy"] = "SameAsRequest",
                ["EmailConfirmation:PublicOrigin"] = "http://localhost:3000",
                ["EmailConfirmation:ConfirmationPath"] = "/confirm-email",
                ["EmailConfirmation:TokenExpiresInHours"] = "24",
                ["EmailTwoFactor:Enabled"] = "true",
                ["EmailTwoFactor:EnableForNewUsers"] = "true",
                ["EmailTwoFactor:CodeExpiresInMinutes"] = "10",
                ["EmailTwoFactor:CodeLength"] = "6",
                ["EmailTwoFactor:MaxFailedAttempts"] = "5",
                ["UiFeatures:GlobalSearchEnabled"] = "true",
                ["UiFeatures:DashboardOverviewEnabled"] = "true",
                ["UiFeatures:AdminNavigationEnabled"] = "true",
                ["UiFeatures:UserManagementNavigationEnabled"] = "true",
                ["UiFeatures:EmailFeatureSectionsEnabled"] = "true"
            };

            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton(EmailSender);
            services.AddSingleton<IAccountEmailSender>(EmailSender);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience",
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes("test-secret-key-1234567890-test-1234567890-extended")),
                    ClockSkew = TimeSpan.Zero
                };
            });
        });
    }
}
