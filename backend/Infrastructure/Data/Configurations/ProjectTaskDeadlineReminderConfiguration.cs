using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskDeadlineReminderConfiguration : IEntityTypeConfiguration<ProjectTaskDeadlineReminder>
{
    public void Configure(EntityTypeBuilder<ProjectTaskDeadlineReminder> builder)
    {
        builder.ToTable("ProjectTaskDeadlineReminders");
        builder.HasKey(reminder => reminder.Id);
        builder.Property(reminder => reminder.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(reminder => new { reminder.ProjectTaskId, reminder.RecipientUserId, reminder.Type, reminder.DueDate }).IsUnique();
        builder.HasOne(reminder => reminder.ProjectTask)
            .WithMany()
            .HasForeignKey(reminder => reminder.ProjectTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(reminder => reminder.RecipientUser)
            .WithMany()
            .HasForeignKey(reminder => reminder.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}