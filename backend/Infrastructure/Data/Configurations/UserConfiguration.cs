using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Email)
            .HasConversion(
                email => email.Value,
                value => EmailAddress.Create(value))
            .IsRequired()
            .HasMaxLength(EmailAddress.MaxLength);
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.ProtectedAuthenticatorSecret).HasMaxLength(2000);
        builder.Property(x => x.ConcurrencyStamp).IsRequired().HasMaxLength(64).IsConcurrencyToken();
        builder.Property(x => x.FailedLoginAttempts).IsRequired();
    }
}
