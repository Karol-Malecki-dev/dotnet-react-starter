using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AuthenticatorLoginChallengeConfiguration : IEntityTypeConfiguration<AuthenticatorLoginChallenge>
{
    public void Configure(EntityTypeBuilder<AuthenticatorLoginChallenge> builder)
    {
        builder.ToTable("AuthenticatorLoginChallenges");
        builder.HasKey(challenge => challenge.Id);
        builder.HasIndex(challenge => new { challenge.UserId, challenge.ExpiresAt });
        builder.HasOne(challenge => challenge.User)
            .WithMany()
            .HasForeignKey(challenge => challenge.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}