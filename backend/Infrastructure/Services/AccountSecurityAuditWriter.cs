using System.Text.Json;
using System.Diagnostics.Metrics;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Persists sanitized account security events without exposing secrets in audit metadata.
/// </summary>
public sealed class AccountSecurityAuditWriter(ApplicationDbContext dbContext, ILogger<AccountSecurityAuditWriter>? logger = null) : IAccountSecurityAuditWriter
{
    private static readonly Meter Meter = new("DotnetReactStarter.SecurityAudit");
    private static readonly Counter<long> PersistenceFailureCounter = Meter.CreateCounter<long>("security_audit.persistence_failures");
    private static readonly HashSet<string> AllowedMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ipAddress",
        "userAgent",
        "reason",
        "authMethod"
    };

    public async Task WriteAsync(AccountSecurityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        var metadata = entry.Metadata?
            .Where(pair => AllowedMetadataKeys.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var metadataJson = metadata is { Count: > 0 } ? JsonSerializer.Serialize(metadata) : null;

        var securityEvent = AccountSecurityEvent.Create(
            entry.EventCode,
            entry.Outcome,
            entry.ActorUserId,
            entry.SubjectUserId,
            entry.OccurredAt,
            entry.CorrelationId,
            metadataJson);

        try
        {
            dbContext.AccountSecurityEvents.Add(securityEvent);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PersistenceFailureCounter.Add(1);
            logger?.LogError(ex, "Account security audit persistence failed for event {EventCode}", entry.EventCode);
        }
    }
}