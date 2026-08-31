namespace Application.DTOs.Admin;

/// <summary>
/// Bounded filters for the administrator security-event read model.
/// </summary>
public sealed class AdminAccountSecurityEventFilterRequestDto
{
    public string? EventCode { get; set; }
    public string? Outcome { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}