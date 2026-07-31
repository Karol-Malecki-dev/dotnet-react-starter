using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskAttachmentConfiguration : IEntityTypeConfiguration<ProjectTaskAttachment>
{
    public void Configure(EntityTypeBuilder<ProjectTaskAttachment> builder)
    {
        builder.ToTable("ProjectTaskAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.OriginalFileName).IsRequired().HasMaxLength(255);
        builder.Property(attachment => attachment.StoredFileName).IsRequired().HasMaxLength(100);
        builder.Property(attachment => attachment.ContentType).IsRequired().HasMaxLength(128);
        builder.Property(attachment => attachment.SizeBytes).IsRequired();
        builder.HasIndex(attachment => new { attachment.ProjectTaskId, attachment.CreatedAt });
        builder.HasOne(attachment => attachment.ProjectTask)
            .WithMany(task => task.Attachments)
            .HasForeignKey(attachment => attachment.ProjectTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(attachment => attachment.UploadedByUser)
            .WithMany()
            .HasForeignKey(attachment => attachment.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}