using Domain.Entities;
using Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class EmailTwoFactorChallengeConfiguration : IEntityTypeConfiguration<EmailTwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<EmailTwoFactorChallenge> builder)
    {
        builder.ToTable("EmailTwoFactorChallenges");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);
        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
