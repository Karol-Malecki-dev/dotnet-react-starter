using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectActivityConfiguration : IEntityTypeConfiguration<ProjectActivity>
{
    public void Configure(EntityTypeBuilder<ProjectActivity> builder)
    {
        builder.ToTable("ProjectActivities");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Type).IsRequired().HasMaxLength(80);
        builder.Property(activity => activity.Description).IsRequired().HasMaxLength(500);
        builder.HasIndex(activity => new { activity.ProjectId, activity.CreatedAt });
        builder.HasOne(activity => activity.Project).WithMany().HasForeignKey(activity => activity.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(activity => activity.ActorUser).WithMany().HasForeignKey(activity => activity.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(activity => activity.ProjectTask).WithMany().HasForeignKey(activity => activity.ProjectTaskId).OnDelete(DeleteBehavior.SetNull);
    }
}