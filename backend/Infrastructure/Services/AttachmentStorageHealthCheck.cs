using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace Infrastructure.Services;

/// <summary>Checks that the configured local attachment storage is available and writable.</summary>
public sealed class AttachmentStorageHealthCheck : IHealthCheck
{
    private readonly IHostEnvironment _environment;
    private readonly AttachmentSettings _settings;

    public AttachmentStorageHealthCheck(
        IHostEnvironment environment,
        IOptions<AttachmentSettings> settings)
    {
        _environment = environment;
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var rootPath = string.IsNullOrWhiteSpace(_settings.RootPath)
            ? Path.Combine(_environment.ContentRootPath, "uploads", "task-attachments")
            : _settings.RootPath;

        try
        {
            Directory.CreateDirectory(rootPath);
            var probePath = Path.Combine(rootPath, $".health-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, "ready");
            File.Delete(probePath);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Attachment storage is unavailable.",
                exception));
        }
    }
}