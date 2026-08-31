namespace Application.Interfaces;

/// <summary>
/// Application port for recording append-only account security events.
/// </summary>
public interface IAccountSecurityAuditWriter
{
    Task WriteAsync(AccountSecurityAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Allowlisted data for one security audit event.
/// </summary>
public sealed record AccountSecurityAuditEntry(
    string EventCode,
    string Outcome,
    Guid? ActorUserId = null,
    Guid? SubjectUserId = null,
    DateTime? OccurredAt = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);