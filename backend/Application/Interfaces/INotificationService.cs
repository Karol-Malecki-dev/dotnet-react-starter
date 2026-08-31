using Domain.Enums;

namespace Application.Interfaces;

public interface INotificationWriter
{
    Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null, bool sendEmail = true, CancellationToken cancellationToken = default, string? deduplicationKey = null);
}
