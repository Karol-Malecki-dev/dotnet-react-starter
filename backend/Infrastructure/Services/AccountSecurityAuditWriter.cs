using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;

namespace Infrastructure.Services;

/// <summary>
/// Persists sanitized account security events without exposing secrets in audit metadata.
/// </summary>
public sealed class AccountSecurityAuditWriter(ApplicationDbContext dbContext) : IAccountSecurityAuditWriter
{
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

        dbContext.AccountSecurityEvents.Add(securityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}