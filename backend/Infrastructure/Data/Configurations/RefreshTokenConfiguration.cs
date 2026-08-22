using Domain.Entities;
using Domain.Entities.JWT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.Property(x => x.UserEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.UserDisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.UserRole).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        builder.Property(x => x.CreatedByIp).IsRequired().HasMaxLength(64);
        builder.Property(x => x.LastUsedByIp).HasMaxLength(64);
        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
