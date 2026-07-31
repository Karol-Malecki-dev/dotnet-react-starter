using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(project => project.Id);
        builder.HasIndex(project => project.OwnerId);
        builder.Property(project => project.Name).IsRequired().HasMaxLength(200);
        builder.Property(project => project.Description).HasMaxLength(2000);
        builder.HasOne<User>().WithMany().HasForeignKey(project => project.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
