using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("ProjectTasks");
        builder.HasKey(task => task.Id);
        builder.HasIndex(task => task.ProjectId);
        builder.HasIndex(task => task.AssignedUserId);
        builder.HasIndex(task => task.CreatedByUserId);
        builder.Property(task => task.Title).IsRequired().HasMaxLength(200);
        builder.Property(task => task.Description).HasMaxLength(2000);
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(task => task.Priority).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasOne(task => task.Project).WithMany(project => project.Tasks).HasForeignKey(task => task.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(task => task.AssignedUser).WithMany().HasForeignKey(task => task.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(task => task.CreatedByUser).WithMany().HasForeignKey(task => task.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
