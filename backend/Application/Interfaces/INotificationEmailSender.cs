namespace Application.Interfaces;

/// <summary>Sends non-security, user-facing notification emails.</summary>
public interface INotificationEmailSender
{
    Task SendAsync(string email, string displayName, string title, string message, CancellationToken cancellationToken = default);
}
