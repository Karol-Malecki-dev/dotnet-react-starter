using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Entities.JWT;
using Domain.Enums.Auth;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

/// <summary>
/// Główny DbContext aplikacji
/// Definiuje wszystkie DbSets (tabele) w bazie danych
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();

    public DbSet<EmailTwoFactorChallenge> EmailTwoFactorChallenges => Set<EmailTwoFactorChallenge>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    /// <summary>
    /// Konfiguracja modeli i relacji między encjami
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.AvatarUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.UserEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.UserDisplayName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.UserRole)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.CreatedByIp)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.LastUsedByIp)
                .HasMaxLength(64);

            entity.Property(x => x.RevocationReason)
                .HasConversion<string>()
                .HasMaxLength(64);

            entity.Property(x => x.ReplacedByTokenHash)
                .HasMaxLength(128);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailConfirmationToken>(entity =>
        {
            entity.ToTable("EmailConfirmationTokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);

            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailTwoFactorChallenge>(entity =>
        {
            entity.ToTable("EmailTwoFactorChallenges");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAt);

            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<PasswordResetRequest>(entity =>
        {
            entity.ToTable("PasswordResetRequests");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAt);
            entity.Property(x => x.ResetType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(128);
            entity.Property(x => x.CodeHash)
                .IsRequired()
                .HasMaxLength(128);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(project => project.Id);
            entity.HasIndex(project => project.OwnerId);
            entity.Property(project => project.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(project => project.Description)
                .HasMaxLength(2000);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.ToTable("ProjectTasks");
            entity.HasKey(task => task.Id);
            entity.HasIndex(task => task.ProjectId);
            entity.HasIndex(task => task.AssignedUserId);
            entity.HasIndex(task => task.CreatedByUserId);
            entity.Property(task => task.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(task => task.Description)
                .HasMaxLength(2000);
            entity.Property(task => task.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(task => task.Priority)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(task => task.AssignedUser)
                .WithMany()
                .HasForeignKey(task => task.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(task => task.CreatedByUser)
                .WithMany()
                .HasForeignKey(task => task.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("ProjectMembers");
            entity.HasKey(member => member.Id);
            entity.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique();
            entity.HasIndex(member => member.UserId);
            entity.Property(member => member.Role)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.HasOne(member => member.Project)
                .WithMany(project => project.Members)
                .HasForeignKey(member => member.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(member => member.User)
                .WithMany(user => user.ProjectMemberships)
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
