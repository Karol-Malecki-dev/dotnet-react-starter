namespace Domain.Entities;

/// <summary>
/// Append-only record of a security-relevant account event.
/// </summary>
public sealed class AccountSecurityEvent
{
    public const int EventCodeMaxLength = 100;
    public const int OutcomeMaxLength = 40;
    public const int CorrelationIdMaxLength = 100;
    public const int MetadataJsonMaxLength = 4000;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; private set; }
    public Guid? SubjectUserId { get; private set; }
    public string EventCode { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public string? CorrelationId { get; private set; }
    public string? MetadataJson { get; private set; }

    private AccountSecurityEvent()
    {
    }

    public static AccountSecurityEvent Create(
        string eventCode,
        string outcome,
        Guid? actorUserId = null,
        Guid? subjectUserId = null,
        DateTime? occurredAt = null,
        string? correlationId = null,
        string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(eventCode) || eventCode.Length > EventCodeMaxLength)
            throw new ArgumentException("Event code is required and must be within the maximum length.", nameof(eventCode));

        if (string.IsNullOrWhiteSpace(outcome) || outcome.Length > OutcomeMaxLength)
            throw new ArgumentException("Outcome is required and must be within the maximum length.", nameof(outcome));

        if (correlationId?.Length > CorrelationIdMaxLength)
            throw new ArgumentException("Correlation ID exceeds the maximum length.", nameof(correlationId));

        if (metadataJson?.Length > MetadataJsonMaxLength)
            throw new ArgumentException("Metadata exceeds the maximum length.", nameof(metadataJson));

        return new AccountSecurityEvent
        {
            ActorUserId = actorUserId,
            SubjectUserId = subjectUserId,
            EventCode = eventCode,
            Outcome = outcome,
            OccurredAt = occurredAt ?? DateTime.UtcNow,
            CorrelationId = correlationId,
            MetadataJson = metadataJson
        };
    }
}