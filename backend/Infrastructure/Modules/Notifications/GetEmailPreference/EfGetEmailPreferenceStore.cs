using Application.DTOs.Notification;
using Application.Modules.Notifications.GetEmailPreference;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications.GetEmailPreference;

public sealed class EfGetEmailPreferenceStore : IGetEmailPreferenceStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfGetEmailPreferenceStore(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<NotificationEmailPreferenceDto> QueryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preference = await _dbContext.NotificationEmailPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        return new NotificationEmailPreferenceDto
        {
            IsEmailEnabled = preference?.IsEmailEnabled ?? true,
            IsTaskDeadlineReminderEmailEnabled = preference?.IsTaskDeadlineReminderEmailEnabled ?? true
        };
    }
}