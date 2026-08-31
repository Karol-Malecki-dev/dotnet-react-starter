using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AccountSecurityEventConfiguration : IEntityTypeConfiguration<AccountSecurityEvent>
{
    public void Configure(EntityTypeBuilder<AccountSecurityEvent> builder)
    {
        builder.ToTable("AccountSecurityEvents");
        builder.HasKey(securityEvent => securityEvent.Id);
        builder.Property(securityEvent => securityEvent.EventCode).IsRequired().HasMaxLength(AccountSecurityEvent.EventCodeMaxLength);
        builder.Property(securityEvent => securityEvent.Outcome).IsRequired().HasMaxLength(AccountSecurityEvent.OutcomeMaxLength);
        builder.Property(securityEvent => securityEvent.CorrelationId).HasMaxLength(AccountSecurityEvent.CorrelationIdMaxLength);
        builder.Property(securityEvent => securityEvent.MetadataJson).HasMaxLength(AccountSecurityEvent.MetadataJsonMaxLength);
        builder.Property(securityEvent => securityEvent.OccurredAt).IsRequired();
        builder.HasIndex(securityEvent => new { securityEvent.SubjectUserId, securityEvent.OccurredAt });
        builder.HasIndex(securityEvent => new { securityEvent.EventCode, securityEvent.OccurredAt });
        builder.HasOne<User>().WithMany().HasForeignKey(securityEvent => securityEvent.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(securityEvent => securityEvent.SubjectUserId).OnDelete(DeleteBehavior.SetNull);
    }
}