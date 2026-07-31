using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskLabelConfiguration : IEntityTypeConfiguration<ProjectTaskLabel>
{
    public void Configure(EntityTypeBuilder<ProjectTaskLabel> builder)
    {
        builder.ToTable("ProjectTaskLabels");
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Name).IsRequired().HasMaxLength(40);
        builder.HasIndex(label => new { label.ProjectTaskId, label.Name }).IsUnique();
        builder.HasOne(label => label.ProjectTask)
            .WithMany(task => task.Labels)
            .HasForeignKey(label => label.ProjectTaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}