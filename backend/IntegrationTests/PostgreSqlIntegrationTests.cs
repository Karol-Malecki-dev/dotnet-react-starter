using Application.Features.Projects;
using Application.DTOs.Notification;
using Application.Interfaces;
using Application.Features.ProjectManagement.Tasks;
using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTask;
using Infrastructure.ProjectManagement.Tasks;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Shared.Responses;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlIntegrationTests
{
    private readonly PostgreSqlWebApplicationFactory _factory;

    public PostgreSqlIntegrationTests(PostgreSqlWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostgreSql_container_applies_all_migrations_and_serves_health_check()
    {
        using var client = _factory.CreateClient();

        var healthResponse = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var knownMigrations = dbContext.Database.GetMigrations();
        Assert.Equal(knownMigrations.Order(), appliedMigrations.Order());
    }

    [Fact]
    public async Task PostgreSql_refresh_rotation_accepts_only_one_concurrent_successor()
    {
        await SeedUserAsync();

        JwtTokens initialTokens;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var serviceProvider = setupScope.ServiceProvider;
            var tokenService = serviceProvider.GetRequiredService<IJwtTokenService>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users
                .SingleAsync(candidate => candidate.Email == EmailAddress.Create("postgres-concurrent@example.com"));

            initialTokens = await tokenService.GenerateTokensAsync(user);
        }

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstTokenService = firstScope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var secondTokenService = secondScope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var refreshResults = await Task.WhenAll(
            firstTokenService.RefreshTokensAsync(initialTokens.RefreshToken),
            secondTokenService.RefreshTokensAsync(initialTokens.RefreshToken));

        Assert.Single(refreshResults, tokens => tokens is not null);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedTokens = await verificationContext.RefreshTokens.ToListAsync();

        Assert.Equal(2, storedTokens.Count);
        Assert.Single(storedTokens, token => token.RevocationReason == RevocationReason.TokenRotated);
        Assert.Single(storedTokens, token => token.RevocationReason == RevocationReason.RefreshTokenReplay);
        Assert.DoesNotContain(storedTokens, token => !token.RevokedAt.HasValue);
    }

    [Fact]
    public async Task PostgreSql_project_update_returns_conflict_for_a_stale_version()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);

        Guid projectId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Concurrency project");
            setupContext.Projects.Add(project);
            await setupContext.SaveChangesAsync();
            projectId = project.Id;
        }

        await using var staleScope = _factory.Services.CreateAsyncScope();
        await using var writerScope = _factory.Services.CreateAsyncScope();
        var staleContext = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var writerContext = writerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleProject = await staleContext.Projects.SingleAsync(project => project.Id == projectId);
        var writerProject = await writerContext.Projects.SingleAsync(project => project.Id == projectId);

        writerProject.Rename("Writer update");
        await writerContext.SaveChangesAsync();

        var service = new DatabaseProjectManagementService(staleContext, new EfProjectMembershipStore(staleContext));
        var result = await service.UpdateProjectAsync(new UpdateProjectCommand(
            ownerId,
            projectId,
            "Stale update",
            null,
            staleProject.ConcurrencyStamp));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedProject = await verificationContext.Projects.SingleAsync(project => project.Id == projectId);
        Assert.Equal("Writer update", persistedProject.Name);
    }

    [Fact]
    public async Task PostgreSql_project_task_update_returns_conflict_for_a_stale_version()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);

        Guid projectId;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Task concurrency project");
            var task = ProjectTask.Create(project.Id, "Original task", null, ProjectTaskPriority.Normal, null, null, ownerId);
            projectId = project.Id;
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        await using var staleScope = _factory.Services.CreateAsyncScope();
        await using var writerScope = _factory.Services.CreateAsyncScope();
        var staleContext = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var writerContext = writerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleTask = await staleContext.ProjectTasks.SingleAsync(task => task.ProjectId == projectId);
        var writerTask = await writerContext.ProjectTasks.SingleAsync(task => task.ProjectId == projectId);

        writerTask.Rename("Writer task update");
        await writerContext.SaveChangesAsync();

        var handler = new UpdateProjectTaskHandler(
            new EfProjectTaskAccess(staleContext),
            new EfProjectTaskCommandStore(staleContext),
            staleScope.ServiceProvider.GetRequiredService<INotificationService>());
        var result = await handler.HandleAsync(new Application.Modules.ProjectTasks.UpdateProjectTask.UpdateProjectTaskCommand(
            ownerId,
            projectId,
            staleTask.Id,
            "Stale task update",
            null,
            ProjectTaskPriority.High,
            null,
            null,
            [],
            staleTask.ConcurrencyStamp));

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedTask = await verificationContext.ProjectTasks.SingleAsync(task => task.Id == staleTask.Id);
        Assert.Equal("Writer task update", persistedTask.Title);
    }

    [Fact]
    public async Task PostgreSql_project_dashboard_due_date_query_uses_the_task_dashboard_index()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await using (var settingCommand = dbContext.Database.GetDbConnection().CreateCommand())
        {
            settingCommand.Transaction = transaction.GetDbTransaction();
            settingCommand.CommandText = "SET LOCAL enable_seqscan = off;";
            await settingCommand.ExecuteNonQueryAsync();
        }

        await using var explainCommand = dbContext.Database.GetDbConnection().CreateCommand();
        explainCommand.Transaction = transaction.GetDbTransaction();
        explainCommand.CommandText = """
            EXPLAIN (FORMAT TEXT)
            SELECT "Id"
            FROM "ProjectTasks"
            WHERE "ProjectId" = @projectId
              AND "Status" <> 'Done'
              AND "DueDate" >= @today
              AND "DueDate" < @nextDay
            ORDER BY "DueDate"
            LIMIT 10;
            """;

        var projectIdParameter = explainCommand.CreateParameter();
        projectIdParameter.ParameterName = "projectId";
        projectIdParameter.Value = Guid.NewGuid();
        explainCommand.Parameters.Add(projectIdParameter);

        var todayParameter = explainCommand.CreateParameter();
        todayParameter.ParameterName = "today";
        todayParameter.Value = DateTime.UtcNow.Date;
        explainCommand.Parameters.Add(todayParameter);

        var nextDayParameter = explainCommand.CreateParameter();
        nextDayParameter.ParameterName = "nextDay";
        nextDayParameter.Value = DateTime.UtcNow.Date.AddDays(8);
        explainCommand.Parameters.Add(nextDayParameter);

        var planLines = new List<string>();
        await using var reader = await explainCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            planLines.Add(reader.GetString(0));
        }

        var plan = string.Join(Environment.NewLine, planLines);
        Assert.Contains("IX_ProjectTasks_ProjectId_Status_DueDate", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSql_project_invitation_acceptance_allows_only_one_concurrent_response()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");
        var projectId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Invitation concurrency project");
            projectId = project.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"invitation-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Invitation Concurrency Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create($"invitation-recipient-{recipientId:N}@example.com"),
                    DisplayName.Create("Invitation Concurrency Recipient"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: recipientId,
                    createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            setupContext.ProjectInvitations.Add(new ProjectInvitation
            {
                ProjectId = project.Id,
                InvitedUserId = recipientId,
                InvitedByUserId = ownerId,
                Role = ProjectMemberRole.Viewer,
                TokenHash = HashToken(token),
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await setupContext.SaveChangesAsync();
        }

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstService = firstScope.ServiceProvider.GetRequiredService<IProjectInvitationApplicationService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<IProjectInvitationApplicationService>();

        var results = await Task.WhenAll(
            firstService.AcceptProjectInvitationAsync(recipientId, token),
            secondService.AcceptProjectInvitationAsync(recipientId, token));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.Status == ProjectOperationStatus.Conflict);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationContext.ProjectMembers.CountAsync(member => member.ProjectId == projectId && member.UserId == recipientId));
        Assert.Equal(ProjectInvitationStatus.Accepted, await verificationContext.ProjectInvitations
            .Where(invitation => invitation.ProjectId == projectId)
            .Select(invitation => invitation.Status)
            .SingleAsync());
    }

    [Fact]
    public async Task PostgreSql_project_invitation_acceptance_rolls_back_when_notification_fails()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");
        var projectId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Invitation rollback project");
            projectId = project.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"rollback-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Invitation Rollback Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create($"rollback-recipient-{recipientId:N}@example.com"),
                    DisplayName.Create("Invitation Rollback Recipient"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: recipientId,
                    createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            setupContext.ProjectInvitations.Add(new ProjectInvitation
            {
                ProjectId = project.Id,
                InvitedUserId = recipientId,
                InvitedByUserId = ownerId,
                Role = ProjectMemberRole.Member,
                TokenHash = HashToken(token),
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = new DatabaseProjectInvitationApplicationService(
                new EfProjectMembershipStore(responseContext),
                new EfProjectInvitationStore(responseContext),
                new FailingNotificationService());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AcceptProjectInvitationAsync(recipientId, token));
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await verificationContext.ProjectMembers.CountAsync(member => member.ProjectId == projectId && member.UserId == recipientId));
        Assert.Equal(ProjectInvitationStatus.Pending, await verificationContext.ProjectInvitations
            .Where(invitation => invitation.ProjectId == projectId)
            .Select(invitation => invitation.Status)
            .SingleAsync());
    }

    [Fact]
    public async Task PostgreSql_project_member_addition_rolls_back_when_notification_fails()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Member rollback project");
            projectId = project.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"member-rollback-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Member Rollback Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create($"member-rollback-user-{memberId:N}@example.com"),
                    DisplayName.Create("Member Rollback User"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: memberId,
                    createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = new DatabaseProjectMembershipApplicationService(
                responseContext,
                new EfProjectMembershipStore(responseContext),
                new FailingNotificationService());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddProjectMemberAsync(ownerId, projectId, memberId));
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationContext.ProjectMembers.CountAsync(member => member.ProjectId == projectId && member.UserId == ownerId));
        Assert.Equal(0, await verificationContext.ProjectMembers.CountAsync(member => member.ProjectId == projectId && member.UserId == memberId));
        Assert.Equal(0, await verificationContext.ProjectActivities.CountAsync(activity => activity.ProjectId == projectId));
    }

    [Fact]
    public async Task PostgreSql_project_member_removal_unassigns_tasks_and_persists_activity()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.Empty;
        var taskId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Member removal project");
            project.AddMember(memberId);
            var task = ProjectTask.Create(
                project.Id,
                "Assigned member task",
                null,
                ProjectTaskPriority.Normal,
                null,
                memberId,
                ownerId);
            projectId = project.Id;
            taskId = task.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"member-removal-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Member Removal Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create($"member-removal-user-{memberId:N}@example.com"),
                    DisplayName.Create("Member Removal User"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: memberId,
                    createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = new DatabaseProjectMembershipApplicationService(
                responseContext,
                new EfProjectMembershipStore(responseContext),
                responseScope.ServiceProvider.GetRequiredService<INotificationService>());

            var result = await service.RemoveProjectMemberAsync(ownerId, projectId, memberId);

            Assert.True(result.IsSuccess);
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verificationContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == memberId));
        var persistedTask = await verificationContext.ProjectTasks.SingleAsync(task => task.Id == taskId);
        Assert.Null(persistedTask.AssignedUserId);
        Assert.Equal(1, await verificationContext.ProjectActivities.CountAsync(activity =>
            activity.ProjectId == projectId && activity.Type == "member.removed"));
    }

    private async Task SeedUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(User.Create(
            EmailAddress.Create("postgres-concurrent@example.com"),
            DisplayName.Create("PostgreSQL Concurrent User"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true));
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedProjectOwnerAsync(Guid ownerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(User.Create(
            EmailAddress.Create($"project-owner-{ownerId:N}@example.com"),
            DisplayName.Create("Project Concurrency Owner"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true,
            id: ownerId,
            createdAt: DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed class FailingNotificationService : INotificationService
    {
        public Task<ApiResponse<NotificationPageDto>> GetUserNotificationsAsync(Guid userId, int pageNumber, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiResponse<int>> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiResponse<NotificationEmailPreferenceDto>> GetEmailPreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiResponse<NotificationEmailPreferenceDto>> UpdateEmailPreferenceAsync(Guid userId, bool? isEmailEnabled, bool? isTaskDeadlineReminderEmailEnabled, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null, bool sendEmail = true, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Notification persistence failed.");
    }
}