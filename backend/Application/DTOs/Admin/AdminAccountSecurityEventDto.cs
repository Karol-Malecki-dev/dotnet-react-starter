namespace Application.DTOs.Admin;

/// <summary>
/// Safe administrative projection of an account security event.
/// </summary>
public sealed class AdminAccountSecurityEventDto
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
}