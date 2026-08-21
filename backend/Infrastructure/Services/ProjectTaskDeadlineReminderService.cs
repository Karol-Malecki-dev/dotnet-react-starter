using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ProjectTaskDeadlineReminderService : IProjectTaskDeadlineReminderService
{
    private static readonly TimeSpan UpcomingWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public ProjectTaskDeadlineReminderService(
        ApplicationDbContext dbContext,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task ProcessDueTasksAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.Add(UpcomingWindow);
        var candidates = await _dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => task.AssignedUserId.HasValue
                && task.DueDate.HasValue
                && task.DueDate.Value <= horizon
                && task.Status != ProjectTaskStatus.Done
                && _dbContext.Projects.Any(project => project.Id == task.ProjectId && !project.IsArchived)
                && task.AssignedUser!.IsActive)
            .Select(task => new { task.Id, task.ProjectId, task.Title, task.AssignedUserId, task.DueDate })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var candidateTaskIds = candidates.Select(task => task.Id).ToList();
        var existingReminders = await _dbContext.ProjectTaskDeadlineReminders
            .AsNoTracking()
            .Where(reminder => candidateTaskIds.Contains(reminder.ProjectTaskId))
            .Select(reminder => new { reminder.ProjectTaskId, reminder.RecipientUserId, reminder.Type, reminder.DueDate })
            .ToListAsync(cancellationToken);
        var reminderKeys = existingReminders
            .Select(reminder => (reminder.ProjectTaskId, reminder.RecipientUserId, reminder.Type, reminder.DueDate))
            .ToHashSet();
        var recipientUserIds = candidates.Select(task => task.AssignedUserId!.Value).Distinct().ToList();
        var deadlineEmailPreferences = await _dbContext.NotificationEmailPreferences
            .AsNoTracking()
            .Where(preference => recipientUserIds.Contains(preference.UserId))
            .ToDictionaryAsync(preference => preference.UserId, preference => preference.IsTaskDeadlineReminderEmailEnabled, cancellationToken);

        foreach (var task in candidates)
        {
            var dueDate = task.DueDate!.Value;
            var recipientUserId = task.AssignedUserId!.Value;
            var reminderType = dueDate < now
                ? ProjectTaskDeadlineReminderType.Overdue
                : ProjectTaskDeadlineReminderType.Approaching;
            var key = (task.Id, recipientUserId, reminderType, dueDate);
            if (!reminderKeys.Add(key))
            {
                continue;
            }

            _dbContext.ProjectTaskDeadlineReminders.Add(new ProjectTaskDeadlineReminder
            {
                ProjectTaskId = task.Id,
                RecipientUserId = recipientUserId,
                Type = reminderType,
                DueDate = dueDate,
                CreatedAt = now
            });

            var isOverdue = reminderType == ProjectTaskDeadlineReminderType.Overdue;
            await _notificationService.CreateAsync(
                recipientUserId,
                isOverdue ? NotificationType.TaskOverdue : NotificationType.TaskDeadlineApproaching,
                isOverdue ? "Task overdue" : "Task deadline approaching",
                isOverdue
                    ? $"The task '{task.Title}' was due on {dueDate:yyyy-MM-dd}."
                    : $"The task '{task.Title}' is due on {dueDate:yyyy-MM-dd}.",
                "ProjectTask",
                task.Id,
                task.ProjectId,
                deadlineEmailPreferences.GetValueOrDefault(recipientUserId, true));
        }
    }
}