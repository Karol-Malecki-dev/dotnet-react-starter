using Infrastructure.Modules.ProjectTasks.DeadlineReminders;
using Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Services;

/// <summary>
/// Reports whether hosted workers completed their most recent processing cycle successfully.
/// </summary>
public sealed class BackgroundWorkerHealthCheck : IHealthCheck
{
    private static readonly (string Name, TimeSpan MaximumAge)[] Workers =
    [
        (NotificationEmailOutboxWorker.WorkerName, TimeSpan.FromMinutes(1)),
        (ProjectTaskDeadlineReminderWorker.WorkerName, TimeSpan.FromHours(2))
    ];

    private readonly BackgroundWorkerHealthState _healthState;

    public BackgroundWorkerHealthCheck(BackgroundWorkerHealthState healthState)
    {
        _healthState = healthState;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var unhealthyWorkers = Workers
            .Select(worker => (worker.Name, Snapshot: _healthState.GetSnapshot(worker.Name), worker.MaximumAge))
            .Where(worker => worker.Snapshot is null
                || !worker.Snapshot.IsHealthy
                || DateTime.UtcNow - worker.Snapshot.LastUpdatedAt > worker.MaximumAge)
            .Select(worker => worker.Name)
            .ToList();

        return Task.FromResult(unhealthyWorkers.Count == 0
            ? HealthCheckResult.Healthy("All background workers are processing normally.")
            : HealthCheckResult.Unhealthy($"Unhealthy background workers: {string.Join(", ", unhealthyWorkers)}."));
    }
}