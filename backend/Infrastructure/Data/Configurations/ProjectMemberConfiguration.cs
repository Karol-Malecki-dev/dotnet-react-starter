using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(member => member.Id);
        builder.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasOne(member => member.Project).WithMany(project => project.Members).HasForeignKey(member => member.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(member => member.User).WithMany(user => user.ProjectMemberships).HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
