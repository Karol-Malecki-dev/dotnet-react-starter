using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class NotificationEmailOutboxMessageConfiguration : IEntityTypeConfiguration<NotificationEmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationEmailOutboxMessage> builder)
    {
        builder.ToTable("NotificationEmailOutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.LastError).HasMaxLength(2000);
        builder.HasIndex(message => new { message.ProcessedAt, message.NextAttemptAt });
        builder.HasIndex(message => message.NotificationId).IsUnique();
        builder.HasOne(message => message.Notification)
            .WithMany()
            .HasForeignKey(message => message.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(message => message.User)
            .WithMany()
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
