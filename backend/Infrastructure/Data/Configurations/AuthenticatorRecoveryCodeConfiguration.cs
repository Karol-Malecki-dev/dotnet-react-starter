using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class AuthenticatorRecoveryCodeConfiguration : IEntityTypeConfiguration<AuthenticatorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AuthenticatorRecoveryCode> builder)
    {
        builder.ToTable("AuthenticatorRecoveryCodes");
        builder.HasKey(code => code.Id);
        builder.Property(code => code.CodeHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(code => new { code.UserId, code.UsedAt });
        builder.HasOne(code => code.User)
            .WithMany(user => user.AuthenticatorRecoveryCodes)
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}