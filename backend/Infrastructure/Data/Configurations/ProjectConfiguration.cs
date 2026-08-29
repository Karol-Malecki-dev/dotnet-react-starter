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
        builder.Property(project => project.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        builder.HasOne<User>().WithMany().HasForeignKey(project => project.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
