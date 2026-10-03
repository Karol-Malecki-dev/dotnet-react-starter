using API.Configurations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Settings;
using System.Net;

namespace API.Services;

public static partial class ProjectServiceCollectionExtensions
{
        private static IServiceCollection AddApplicationOptions(
            this IServiceCollection services,
            IConfiguration configuration,
            bool isProduction)
        {
            services.AddOptions<JwtSettings>()
                .Bind(configuration.GetSection("Jwt"))
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret), "JWT Secret is required.")
                .Validate(settings => settings.Secret.Length >= 32, "JWT Secret must be at least 32 characters long.")
                .Validate(settings => !isProduction || !IsKnownExampleJwtSecret(settings.Secret),
                    "A non-example JWT Secret must be configured in production.")
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
                .Validate(settings => IsValidCookieName(settings.RefreshTokenCookieName),
                    "RefreshTokenCookieName contains invalid characters.")
                .Validate(settings => IsValidCookiePath(settings.RefreshTokenCookiePath),
                    "RefreshTokenCookiePath must be an absolute cookie path without control characters.")
                .Validate(settings => IsValidCookieDomain(settings.RefreshTokenCookieDomain),
                    "RefreshTokenCookieDomain must contain only a host name or IP address.")
                .Validate(settings => !isProduction
                    || string.Equals(settings.RefreshTokenCookieSecurePolicy, nameof(CookieSecurePolicy.Always), StringComparison.OrdinalIgnoreCase),
                    "Production refresh token cookies must use CookieSecurePolicy=Always.")
                .ValidateOnStart();

            services.AddOptions<CorsSettings>()
                .Bind(configuration.GetSection("Cors"))
                .Validate(settings => settings.AllowedOrigins.Length > 0, "At least one CORS allowed origin is required.")
                .Validate(settings => settings.AllowedOrigins.All(IsValidCorsOrigin),
                    "All CORS allowed origins must be absolute HTTP or HTTPS origins without paths or credentials.")
                .Validate(settings => !settings.AllowCredentials || settings.AllowedOrigins.All(origin => origin != "*"),
                    "Wildcard CORS origins cannot be used when credentials are enabled.")
                .Validate(settings => !isProduction || settings.AllowedOrigins.All(IsHttpsOrigin),
                    "Production CORS origins must use HTTPS.")
                .ValidateOnStart();

            services.AddOptions<DataProtectionSettings>()
                .Bind(configuration.GetSection("DataProtection"))
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.ApplicationName),
                    "Data Protection application name is required.")
                .Validate(settings => string.IsNullOrWhiteSpace(settings.KeyRingPath) || Path.IsPathRooted(settings.KeyRingPath),
                    "Data Protection key ring path must be absolute when configured.")
                .Validate(settings => !isProduction || !string.IsNullOrWhiteSpace(settings.KeyRingPath),
                    "Data Protection key ring path is required in production.")
                .ValidateOnStart();

            services.AddOptions<DatabaseSettings>()
                .Bind(configuration.GetSection("Database"))
                .Validate(settings => !isProduction || !settings.ApplyMigrationsOnStartup,
                    "Automatic database migrations must be disabled in production. Run the API image with --migrate-only before starting the application.")
                .ValidateOnStart();

            services.AddOptions<EmailConfirmationSettings>()
                .Bind(configuration.GetSection("EmailConfirmation"))
                .Validate(settings => Uri.TryCreate(settings.PublicOrigin, UriKind.Absolute, out _),
                    "Email confirmation public origin must be an absolute URL.")
                .Validate(settings => !isProduction || IsHttpsOrigin(settings.PublicOrigin),
                    "Email confirmation public origin must use HTTPS in production.")
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

            services.AddOptions<AuthSecuritySettings>()
                .Bind(configuration.GetSection("AuthSecurity"))
                .Validate(settings => settings.RateLimitPermitLimit > 0,
                    "Auth rate-limit permit limit must be greater than 0.")
                .Validate(settings => settings.RateLimitWindowSeconds > 0,
                    "Auth rate-limit window must be greater than 0 seconds.")
                .Validate(settings => settings.MaxFailedLoginAttempts > 0,
                    "Maximum failed login attempts must be greater than 0.")
                .Validate(settings => settings.LockoutDurationMinutes > 0,
                    "Lockout duration must be greater than 0 minutes.")
                .ValidateOnStart();

            services.AddOptions<AttachmentSettings>()
                .Bind(configuration.GetSection("Attachments"))
                .Validate(settings => string.Equals(settings.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(settings.StorageProvider, "S3", StringComparison.OrdinalIgnoreCase),
                    "Attachment storage provider must be Local or S3.")
                .Validate(settings => !isProduction
                    || string.Equals(settings.StorageProvider, "S3", StringComparison.OrdinalIgnoreCase),
                    "Production attachment storage must use the S3 provider.")
                .Validate(settings => !isProduction
                    || !string.Equals(settings.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(settings.RootPath) && Path.IsPathRooted(settings.RootPath)),
                    "An absolute attachment storage root is required in production.")
                .Validate(settings => !string.Equals(settings.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(settings.RootPath)
                    || Path.IsPathRooted(settings.RootPath),
                    "Attachment storage root must be absolute when configured.")
                .Validate(settings => !string.Equals(settings.StorageProvider, "S3", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(settings.S3BucketName),
                    "An S3 bucket name is required when S3 attachment storage is selected.")
                .Validate(settings => !string.Equals(settings.StorageProvider, "S3", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(settings.S3ServiceUrl)
                    || !string.IsNullOrWhiteSpace(settings.S3Region),
                    "An S3 region is required when no custom S3 service URL is configured.")
                .Validate(settings => string.IsNullOrWhiteSpace(settings.S3ServiceUrl)
                    || Uri.TryCreate(settings.S3ServiceUrl, UriKind.Absolute, out var endpoint)
                    && endpoint.Scheme is "http" or "https",
                    "The S3 service URL must be an absolute HTTP or HTTPS URL.")
                .Validate(settings => string.IsNullOrWhiteSpace(settings.S3AccessKey) == string.IsNullOrWhiteSpace(settings.S3SecretKey),
                    "S3 access and secret keys must either both be configured or both be omitted.")
                .Validate(settings => settings.MaxFileSizeBytes > 0,
                    "Attachment maximum file size must be greater than 0.")
                .Validate(settings => settings.MaxCountPerTask > 0,
                    "Attachment maximum count per task must be greater than 0.")
                .Validate(settings => settings.MaxBytesPerTask >= settings.MaxFileSizeBytes,
                    "Attachment maximum bytes per task must be at least the maximum file size.")
                .Validate(settings => !isProduction || settings.RequireMalwareScan,
                    "Attachment malware scanning must be required in production.")
                .Validate(settings => !isProduction || !string.IsNullOrWhiteSpace(settings.MalwareScannerHost),
                    "An attachment malware scanner host is required in production.")
                .Validate(settings => settings.MalwareScannerPort is > 0 and <= 65535,
                    "Attachment malware scanner port must be between 1 and 65535.")
                .Validate(settings => settings.MalwareScannerTimeoutSeconds > 0,
                    "Attachment malware scanner timeout must be greater than 0 seconds.")
                .ValidateOnStart();

            services.AddOptions<EmailDeliverySettings>()
                .Bind(configuration.GetSection("EmailDelivery"))
                .Validate(settings => !isProduction || settings.Enabled,
                    "Email delivery must be enabled in production.")
                .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.Host),
                    "Email delivery host is required when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || settings.Port > 0,
                    "Email delivery port must be greater than 0 when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || settings.TimeoutSeconds is > 0 and <= 300,
                    "Email delivery timeout must be between 1 and 300 seconds when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.FromAddress),
                    "Email delivery from address is required when email delivery is enabled.")
                .ValidateOnStart();

            return services;
        }

        private static IServiceCollection AddDataProtectionInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var settings = configuration.GetSection("DataProtection").Get<DataProtectionSettings>()
                ?? new DataProtectionSettings();

            var dataProtection = services
                .AddDataProtection()
                .SetApplicationName(settings.ApplicationName);

            if (!string.IsNullOrWhiteSpace(settings.KeyRingPath))
            {
                dataProtection.PersistKeysToFileSystem(new DirectoryInfo(settings.KeyRingPath));
            }

            return services;
        }

        private static IServiceCollection AddForwardedHeadersInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration,
            bool isProduction)
        {
            services.AddOptions<ForwardedHeadersSettings>()
                .Bind(configuration.GetSection("ForwardedHeaders"))
                .Validate(settings => !isProduction || settings.Enabled,
                    "Forwarded headers must be enabled in production.")
                .Validate(settings => settings.ForwardLimit > 0,
                    "Forwarded headers forward limit must be greater than 0.")
                .Validate(settings => settings.KnownProxies.All(IsValidIpAddress),
                    "Forwarded headers known proxies must be valid IP address literals.")
                .Validate(settings => settings.KnownNetworks.All(IsValidIpNetwork),
                    "Forwarded headers known networks must use valid CIDR notation.")
                .Validate(settings => !settings.Enabled
                    || settings.KnownProxies.Length > 0
                    || settings.KnownNetworks.Length > 0,
                    "At least one trusted proxy or network is required when forwarded headers are enabled.")
                .ValidateOnStart();

            var settings = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersSettings>()
                ?? new ForwardedHeadersSettings();

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = settings.Enabled
                    ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                    : ForwardedHeaders.None;
                options.ForwardLimit = settings.ForwardLimit;
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();

                foreach (var knownProxy in settings.KnownProxies)
                {
                    options.KnownProxies.Add(IPAddress.Parse(knownProxy));
                }

                foreach (var knownNetwork in settings.KnownNetworks)
                {
                    options.KnownNetworks.Add(ParseIpNetwork(knownNetwork));
                }
            });

            return services;
        }

}
