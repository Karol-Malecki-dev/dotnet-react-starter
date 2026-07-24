using API.Configurations;
using API.Filters;
using Application.Interfaces;
using Application.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Settings;
using System.Threading.RateLimiting;

namespace API.Services
{
    /// <summary>
    /// Registers API composition-root services in one place so Program.cs stays thin.
    /// </summary>
    public static class ProjectServiceCollectionExtensions
    {
        /// <summary>
        /// Registers controllers, options, authentication, infrastructure, and application services.
        /// </summary>
        public static IServiceCollection AddProjectServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApiPipelineServices();
            services.AddApplicationOptions(configuration);
            services.AddPersistence(configuration);
            services.AddAuthenticationInfrastructure();
            services.AddApplicationServices();
            services.AddCorsPolicy(configuration);
            services.AddRateLimitingInfrastructure();

            return services;
        }

        private static IServiceCollection AddApiPipelineServices(this IServiceCollection services)
        {
            services.AddControllers();
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<global::Program>();

            services.AddHealthChecks();
            services.AddHttpContextAccessor();
            services.AddSwaggerGen();
            services.AddAuthorization();

            return services;
        }

        private static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<JwtSettings>()
                .Bind(configuration.GetSection("Jwt"))
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret), "JWT Secret is required.")
                .Validate(settings => settings.Secret.Length >= 32, "JWT Secret must be at least 32 characters long.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JWT Issuer is required.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JWT Audience is required.")
                .Validate(settings => settings.AccessTokenExpiresInMinutes > 0, "AccessTokenExpiresInMinutes must be greater than 0.")
                .Validate(settings => settings.RefreshTokenExpiresInDays > 0, "RefreshTokenExpiresInDays must be greater than 0.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.RefreshTokenCookieName), "RefreshTokenCookieName is required.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.RefreshTokenCookiePath), "RefreshTokenCookiePath is required.")
                .Validate(settings => Enum.TryParse<SameSiteMode>(settings.RefreshTokenCookieSameSite, true, out _), "RefreshTokenCookieSameSite must be one of: Strict, Lax, None, Unspecified.")
                .Validate(settings => Enum.TryParse<CookieSecurePolicy>(settings.RefreshTokenCookieSecurePolicy, true, out _), "RefreshTokenCookieSecurePolicy must be one of: Always, SameAsRequest, None.")
                .Validate(settings => !string.Equals(settings.RefreshTokenCookieSameSite, "None", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(settings.RefreshTokenCookieSecurePolicy, "None", StringComparison.OrdinalIgnoreCase),
                    "Refresh token cookies with SameSite=None must not use CookieSecurePolicy=None.")
                .ValidateOnStart();

            services.AddOptions<CorsSettings>()
                .Bind(configuration.GetSection("Cors"))
                .Validate(settings => settings.AllowedOrigins.Length > 0, "At least one CORS allowed origin is required.")
                .Validate(settings => settings.AllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
                    "All CORS allowed origins must be absolute URLs.")
                .Validate(settings => !settings.AllowCredentials || settings.AllowedOrigins.All(origin => origin != "*"),
                    "Wildcard CORS origins cannot be used when credentials are enabled.")
                .ValidateOnStart();

            services.AddOptions<EmailConfirmationSettings>()
                .Bind(configuration.GetSection("EmailConfirmation"))
                .Validate(settings => Uri.TryCreate(settings.PublicOrigin, UriKind.Absolute, out _),
                    "Email confirmation public origin must be an absolute URL.")
                .Validate(settings => settings.TokenExpiresInHours > 0,
                    "Email confirmation token lifetime must be greater than 0 hours.")
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConfirmationPath),
                    "Email confirmation path is required.")
                .ValidateOnStart();

            services.AddOptions<EmailTwoFactorSettings>()
                .Bind(configuration.GetSection("EmailTwoFactor"))
                .Validate(settings => settings.CodeExpiresInMinutes > 0,
                    "Email 2FA code lifetime must be greater than 0 minutes.")
                .Validate(settings => settings.CodeLength >= 4 && settings.CodeLength <= 10,
                    "Email 2FA code length must be between 4 and 10 digits.")
                .Validate(settings => settings.MaxFailedAttempts > 0,
                    "Email 2FA maximum failed attempts must be greater than 0.")
                .ValidateOnStart();

            services.AddOptions<EmailDeliverySettings>()
                .Bind(configuration.GetSection("EmailDelivery"))
                .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.Host),
                    "Email delivery host is required when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || settings.Port > 0,
                    "Email delivery port must be greater than 0 when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.FromAddress),
                    "Email delivery from address is required when email delivery is enabled.")
                .ValidateOnStart();

            return services;
        }

        private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DbConnectionString"]
                ?? throw new InvalidOperationException("Connection string not found");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            return services;
        }

        private static IServiceCollection AddAuthenticationInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer();

            return services;
        }

        private static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ValidationFilterAttribute>();

            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, DatabaseAuthService>();
            services.AddScoped<IUserService, DatabaseUserService>();
            services.AddScoped<IAdminService, DatabaseAdminService>();

            services.AddScoped<LoggingAccountEmailSender>();
            services.AddScoped<MailKitAccountEmailSender>();
            services.AddScoped<IAccountEmailSender>(serviceProvider =>
            {
                var emailDeliverySettings = serviceProvider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value;

                return emailDeliverySettings.Enabled
                    ? serviceProvider.GetRequiredService<MailKitAccountEmailSender>()
                    : serviceProvider.GetRequiredService<LoggingAccountEmailSender>();
            });

            return services;
        }

        private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
        {
            var corsSettings = configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();

            services.AddCors(options =>
            {
                options.AddPolicy("ReactApp", policy =>
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader();

                    if (corsSettings.AllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                });
            });

            return services;
        }

        private static IServiceCollection AddRateLimitingInfrastructure(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 5;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 0;
                });
            });

            return services;
        }
    }
}
