using API.Configurations;
using API.Filters;
using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Application.Interfaces;
using Application.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.Modules.Notifications;
using Infrastructure.Modules.ProjectTasks;
using Infrastructure.Modules.Projects;
using Application.Modules.Workspace.SearchWorkspace;
using Infrastructure.Modules.Workspace.SearchWorkspace;
using Infrastructure.ProjectManagement.Tasks;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Responses;
using Shared.Settings;
using System.Net;
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
        public static IServiceCollection AddProjectServices(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment? hostEnvironment = null)
        {
            var isProduction = hostEnvironment?.IsProduction()
                ?? string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], Environments.Production, StringComparison.OrdinalIgnoreCase);

            services.AddApiPipelineServices();
            services.AddApplicationOptions(configuration, isProduction);
            services.AddDataProtectionInfrastructure(configuration);
            services.AddForwardedHeadersInfrastructure(configuration);
            services.AddPersistence(configuration);
            services.AddAuthenticationInfrastructure();
            services.AddApplicationServices();
            services.AddScoped<IAccountSecurityAuditWriter, AccountSecurityAuditWriter>();
            services.AddCorsPolicy(configuration);
            services.AddRateLimitingInfrastructure(configuration);

            return services;
        }

        private static IServiceCollection AddApiPipelineServices(this IServiceCollection services)
        {
            services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                        new BadRequestObjectResult(ValidationResponseFactory.Create(context.ModelState));
                });
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<global::Program>();

            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "database"])
                .AddCheck<BackgroundWorkerHealthCheck>("background-workers", tags: ["workers"])
                .AddCheck<AttachmentStorageHealthCheck>("attachment-storage", tags: ["ready", "storage"])
                .AddCheck<AttachmentMalwareScannerHealthCheck>("attachment-malware-scanner", tags: ["ready", "storage"]);
            services.AddHttpContextAccessor();
            services.AddSwaggerGen();
            services.AddAuthorization();

            return services;
        }

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
                .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.Host),
                    "Email delivery host is required when email delivery is enabled.")
                .Validate(settings => !settings.Enabled || settings.Port > 0,
                    "Email delivery port must be greater than 0 when email delivery is enabled.")
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
            IConfiguration configuration)
        {
            services.AddOptions<ForwardedHeadersSettings>()
                .Bind(configuration.GetSection("ForwardedHeaders"))
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
            services.AddScoped<INotificationWriter, DatabaseNotificationWriter>();
            services.AddScoped<ICollaborationNotificationWriter, CollaborationNotificationWriter>();
            services.AddScoped<LoggingNotificationEmailSender>();
            services.AddSingleton<BackgroundWorkerHealthState>();
            services.AddScoped<MailKitNotificationEmailSender>();
            services.AddScoped<INotificationEmailSender>(serviceProvider =>
            {
                var emailDeliverySettings = serviceProvider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value;
                return emailDeliverySettings.Enabled
                    ? serviceProvider.GetRequiredService<MailKitNotificationEmailSender>()
                    : serviceProvider.GetRequiredService<LoggingNotificationEmailSender>();
            });
            services.AddHostedService<NotificationEmailOutboxWorker>();
            services.AddScoped<IAdminService, DatabaseAdminService>();
            services.AddScoped<ISearchWorkspaceHandler, SearchWorkspaceHandler>();
            services.AddScoped<ISearchWorkspaceStore, EfSearchWorkspaceStore>();
            services.AddNotificationsModule();
            services.AddProjectTasksModule();
            services.AddProjectsModule();

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

        private static IServiceCollection AddRateLimitingInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var authSecuritySettings = configuration.GetSection("AuthSecurity").Get<AuthSecuritySettings>() ?? new AuthSecuritySettings();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = static async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        ApiResponse.Error(
                            StatusCodes.Status429TooManyRequests,
                            "Too many requests. Please try again later."),
                        cancellationToken);
                };

                options.AddPolicy("AuthPolicy", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        GetAuthRateLimitPartitionKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authSecuritySettings.RateLimitPermitLimit,
                            Window = TimeSpan.FromSeconds(authSecuritySettings.RateLimitWindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            return services;
        }

        private static string GetAuthRateLimitPartitionKey(HttpContext httpContext)
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var endpoint = httpContext.Request.Path.Value ?? "/";
            return $"{clientIp}:{endpoint}";
        }

        private static bool IsKnownExampleJwtSecret(string secret)
            => string.Equals(secret, "local-development-secret-change-before-production-123456789", StringComparison.Ordinal)
                || string.Equals(secret, "local-development-only-secret-change-before-production-123456789", StringComparison.Ordinal)
                || string.Equals(secret, "change-this-to-a-long-random-secret-at-least-32-characters", StringComparison.OrdinalIgnoreCase);

        private static bool IsValidCookieName(string name)
            => !string.IsNullOrWhiteSpace(name)
                && name.All(character => !char.IsControl(character)
                    && !char.IsWhiteSpace(character)
                    && !"()<>@,;:\\\"/[]?={}".Contains(character));

        private static bool IsValidCookiePath(string path)
            => !string.IsNullOrWhiteSpace(path)
                && path.StartsWith("/", StringComparison.Ordinal)
                && path.All(character => !char.IsControl(character));

        private static bool IsValidCookieDomain(string? domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return true;
            }

            var normalizedDomain = domain.TrimStart('.');
            return normalizedDomain.Length > 0
                && !normalizedDomain.Contains('/', StringComparison.Ordinal)
                && !normalizedDomain.Contains(':', StringComparison.Ordinal)
                && Uri.CheckHostName(normalizedDomain) != UriHostNameType.Unknown;
        }

        private static bool IsValidCorsOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrWhiteSpace(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo)
                && string.IsNullOrEmpty(uri.Query)
                && string.IsNullOrEmpty(uri.Fragment)
                && uri.AbsolutePath == "/";
        }

        private static bool IsValidIpAddress(string value)
            => IPAddress.TryParse(value, out _);

        private static bool IsValidIpNetwork(string value)
            => TryParseIpNetwork(value, out _);

        private static Microsoft.AspNetCore.HttpOverrides.IPNetwork ParseIpNetwork(string value)
        {
            if (!TryParseIpNetwork(value, out var network))
            {
                throw new InvalidOperationException($"Invalid forwarded-header network '{value}'.");
            }

            return network;
        }

        private static bool TryParseIpNetwork(
            string value,
            out Microsoft.AspNetCore.HttpOverrides.IPNetwork network)
        {
            network = null!;
            var separatorIndex = value.LastIndexOf("/", StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                return false;
            }

            if (!IPAddress.TryParse(value[..separatorIndex], out var prefix)
                || !int.TryParse(value[(separatorIndex + 1)..], out var prefixLength))
            {
                return false;
            }

            var maxPrefixLength = prefix.GetAddressBytes().Length * 8;
            if (prefixLength < 0 || prefixLength > maxPrefixLength)
            {
                return false;
            }

            network = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength);
            return true;
        }
    }
}
