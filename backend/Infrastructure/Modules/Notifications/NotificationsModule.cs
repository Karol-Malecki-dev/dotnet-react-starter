using Application.Modules.Notifications.Commands;
using Application.Modules.Notifications.GetEmailPreference;
using Application.Modules.Notifications.GetUnreadCount;
using Application.Modules.Notifications.ListNotifications;
using Infrastructure.Modules.Notifications.Commands;
using Infrastructure.Modules.Notifications.GetEmailPreference;
using Infrastructure.Modules.Notifications.GetUnreadCount;
using Infrastructure.Modules.Notifications.ListNotifications;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.AddScoped<IListNotificationsStore, EfListNotificationsStore>();
        services.AddScoped<IListNotificationsHandler, ListNotificationsHandler>();
        services.AddScoped<IGetUnreadCountStore, EfGetUnreadCountStore>();
        services.AddScoped<IGetUnreadCountHandler, GetUnreadCountHandler>();
        services.AddScoped<IGetEmailPreferenceStore, EfGetEmailPreferenceStore>();
        services.AddScoped<IGetEmailPreferenceHandler, GetEmailPreferenceHandler>();
        services.AddScoped<IMarkNotificationAsReadStore, EfMarkNotificationAsReadStore>();
        services.AddScoped<IMarkNotificationAsReadHandler, MarkNotificationAsReadHandler>();
        services.AddScoped<IMarkAllNotificationsAsReadStore, EfMarkAllNotificationsAsReadStore>();
        services.AddScoped<IMarkAllNotificationsAsReadHandler, MarkAllNotificationsAsReadHandler>();
        services.AddScoped<IUpdateNotificationEmailPreferenceStore, EfUpdateNotificationEmailPreferenceStore>();
        services.AddScoped<IUpdateNotificationEmailPreferenceHandler, UpdateNotificationEmailPreferenceHandler>();

        return services;
    }
}