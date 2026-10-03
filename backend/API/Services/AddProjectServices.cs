using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Application.Interfaces;
using Infrastructure.Dispatching;
using Infrastructure.Services;
using System.Net;

namespace API.Services
{
    /// <summary>
    /// Registers API composition-root services in one place so Program.cs stays thin.
    /// </summary>
    public static partial class ProjectServiceCollectionExtensions
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
            services.AddForwardedHeadersInfrastructure(configuration, isProduction);
            services.AddPersistence(configuration);
            services.AddAuthenticationInfrastructure();
            services.AddApplicationDispatch();
            services.AddApplicationServices();
            services.AddScoped<IAccountSecurityAuditWriter, AccountSecurityAuditWriter>();
            services.AddCorsPolicy(configuration);
            services.AddRateLimitingInfrastructure(configuration);

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
    
            private static bool IsHttpsOrigin(string origin)
                => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && !string.IsNullOrWhiteSpace(uri.Host)
                    && string.IsNullOrEmpty(uri.UserInfo)
                    && string.IsNullOrEmpty(uri.Query)
                    && string.IsNullOrEmpty(uri.Fragment)
                    && uri.AbsolutePath == "/";
    
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
