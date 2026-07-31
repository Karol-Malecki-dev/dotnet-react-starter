using System.Collections.Concurrent;

namespace Infrastructure.Services;

public sealed class BackgroundWorkerHealthState
{
    private readonly ConcurrentDictionary<string, BackgroundWorkerHealthSnapshot> _snapshots = new();

    public void ReportSuccess(string workerName) => _snapshots[workerName] = new BackgroundWorkerHealthSnapshot(true, DateTime.UtcNow, null);

    public void ReportFailure(string workerName, Exception exception) => _snapshots[workerName] = new BackgroundWorkerHealthSnapshot(
        false,
        DateTime.UtcNow,
        exception.Message[..Math.Min(exception.Message.Length, 200)]);

    public BackgroundWorkerHealthSnapshot? GetSnapshot(string workerName) =>
        _snapshots.TryGetValue(workerName, out var snapshot) ? snapshot : null;
}

public sealed record BackgroundWorkerHealthSnapshot(bool IsHealthy, DateTime LastUpdatedAt, string? LastError);