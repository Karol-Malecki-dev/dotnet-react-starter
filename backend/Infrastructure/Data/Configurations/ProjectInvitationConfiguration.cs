using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectInvitationConfiguration : IEntityTypeConfiguration<ProjectInvitation>
{
    public void Configure(EntityTypeBuilder<ProjectInvitation> builder)
    {
        builder.ToTable("ProjectInvitations");
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(invitation => invitation.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(invitation => invitation.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(invitation => invitation.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();
        builder.HasIndex(invitation => new { invitation.InvitedUserId, invitation.Status, invitation.ExpiresAt });
        builder.HasIndex(invitation => new { invitation.ProjectId, invitation.Status });
        builder.HasIndex(invitation => new
        {
            invitation.ProjectId,
            invitation.InvitedUserId,
            invitation.Status
        })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
        builder.HasOne(invitation => invitation.Project)
            .WithMany(project => project.Invitations)
            .HasForeignKey(invitation => invitation.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(invitation => invitation.InvitedUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
