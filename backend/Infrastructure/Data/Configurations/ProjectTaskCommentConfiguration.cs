using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ProjectTaskCommentConfiguration : IEntityTypeConfiguration<ProjectTaskComment>
{
    public void Configure(EntityTypeBuilder<ProjectTaskComment> builder)
    {
        builder.ToTable("ProjectTaskComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Content).IsRequired().HasMaxLength(2000);
        builder.HasIndex(comment => new { comment.ProjectTaskId, comment.CreatedAt });
        builder.HasOne(comment => comment.ProjectTask)
            .WithMany(task => task.Comments)
            .HasForeignKey(comment => comment.ProjectTaskId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(comment => comment.AuthorUser)
            .WithMany()
            .HasForeignKey(comment => comment.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
