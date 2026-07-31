using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Title).IsRequired().HasMaxLength(160);
        builder.Property(notification => notification.Message).IsRequired().HasMaxLength(1000);
        builder.Property(notification => notification.ResourceType).HasMaxLength(80);
        builder.HasIndex(notification => new { notification.UserId, notification.ReadAt, notification.CreatedAt });
        builder.HasIndex(notification => notification.ProjectId);
        builder.HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
