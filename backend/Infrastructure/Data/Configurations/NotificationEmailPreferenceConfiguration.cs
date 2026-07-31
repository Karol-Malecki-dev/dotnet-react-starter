using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class NotificationEmailPreferenceConfiguration : IEntityTypeConfiguration<NotificationEmailPreference>
{
    public void Configure(EntityTypeBuilder<NotificationEmailPreference> builder)
    {
        builder.ToTable("NotificationEmailPreferences");
        builder.HasKey(preference => preference.UserId);
        builder.HasOne(preference => preference.User)
            .WithOne()
            .HasForeignKey<NotificationEmailPreference>(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
