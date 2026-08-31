using Microsoft.Extensions.Diagnostics.HealthChecks;
using Application.Modules.ProjectTasks.Attachments;

namespace Infrastructure.Services;

/// <summary>Checks that the configured attachment storage is available and writable.</summary>
public sealed class AttachmentStorageHealthCheck : IHealthCheck
{
    private readonly IProjectTaskAttachmentStorage _storage;

    public AttachmentStorageHealthCheck(
        IProjectTaskAttachmentStorage storage)
    {
        _storage = storage;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var probeKey = $"{Guid.NewGuid():N}.txt";
        try
        {
            await using var content = new MemoryStream("ready"u8.ToArray());
            await _storage.SaveAsync(content, probeKey, cancellationToken);
            await _storage.DeleteAsync(probeKey, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Attachment storage is unavailable.",
                exception);
        }
    }
}