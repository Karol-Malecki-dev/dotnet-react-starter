using Domain.Enums;

namespace Application.Interfaces;

public interface INotificationWriter
{
    Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null, bool sendEmail = true, CancellationToken cancellationToken = default, string? deduplicationKey = null);
}

/// <summary>
/// Stages a collaboration notification in the caller's unit of work without saving it.
/// </summary>
public interface ICollaborationNotificationWriter
{
    Task StageAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string resourceType,
        Guid resourceId,
        Guid projectId,
        string deduplicationKey,
        CancellationToken cancellationToken = default);
}
