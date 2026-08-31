using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskAttachmentCleanupMessageConfiguration : IEntityTypeConfiguration<ProjectTaskAttachmentCleanupMessage>
{
    public void Configure(EntityTypeBuilder<ProjectTaskAttachmentCleanupMessage> builder)
    {
        builder.ToTable("ProjectTaskAttachmentCleanupMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.StoredFileName)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(message => message.LastError)
            .HasMaxLength(2000);
        builder.HasIndex(message => new { message.ProcessedAt, message.NextAttemptAt });
    }
}
