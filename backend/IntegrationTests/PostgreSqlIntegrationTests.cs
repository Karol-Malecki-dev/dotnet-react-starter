using Application.Features.Projects;
using Application.Interfaces;
using Application.Features.ProjectManagement.Tasks;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.Assignments;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Application.Modules.ProjectTasks.DeleteProjectTask;
using Application.Modules.Projects.AddProjectMember;
using Application.Modules.Projects.AcceptProjectInvitation;
using Application.Modules.Projects.CreateProjectInvitation;
using Application.Modules.Projects.Invitations;
using Application.Modules.Projects.RemoveProjectMember;
using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.Attachments;
using Infrastructure.Modules.ProjectTasks.Assignments;
using Infrastructure.Modules.ProjectTasks.CreateProjectTask;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTask;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTask;
using Infrastructure.Modules.Projects.AddProjectMember;
using Infrastructure.Modules.Projects.AcceptProjectInvitation;
using Infrastructure.Modules.Projects.CreateProjectInvitation;
using Infrastructure.Modules.Projects.Invitations;
using Infrastructure.Modules.Projects.RemoveProjectMember;
using Infrastructure.Modules.Projects.UpdateProject;
using Infrastructure.ProjectManagement.Tasks;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Settings;
using System.Net;
using System.Security.Cryptography;
using System.Text;

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
        Assert.Contains(
            "20260830232316_AddProjectTaskAttachmentCleanup",
            appliedMigrations);
        Assert.Contains(
            "20260831100826_PreventConcurrentPendingProjectInvitations",
            appliedMigrations);
    }

    [Fact]
    public async Task PostgreSql_readiness_reports_attachment_storage_as_healthy()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body.Trim(), ignoreCase: true);
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

        var handler = new UpdateProjectHandler(new EfUpdateProjectStore(staleContext));
        var result = await handler.HandleAsync(new Application.Modules.Projects.UpdateProject.UpdateProjectCommand(
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
            staleScope.ServiceProvider.GetRequiredService<IProjectTaskAssignmentNotificationWriter>());
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
        var firstHandler = firstScope.ServiceProvider.GetRequiredService<IAcceptProjectInvitationHandler>();
        var secondHandler = secondScope.ServiceProvider.GetRequiredService<IAcceptProjectInvitationHandler>();

        var results = await Task.WhenAll(
            firstHandler.HandleAsync(new AcceptProjectInvitationCommand(recipientId, token)),
            secondHandler.HandleAsync(new AcceptProjectInvitationCommand(recipientId, token)));

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
    public async Task PostgreSql_project_invitation_creation_replaces_an_expired_pending_invitation()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var recipientEmail = $"reinvite-recipient-{recipientId:N}@example.com";
        var projectId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Invitation replacement project");
            projectId = project.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"reinvite-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Reinvite Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create(recipientEmail),
                    DisplayName.Create("Reinvite Recipient"),
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
                TokenHash = HashToken(Guid.NewGuid().ToString("N")),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var createScope = _factory.Services.CreateAsyncScope())
        {
            var handler = createScope.ServiceProvider.GetRequiredService<ICreateProjectInvitationHandler>();

            var result = await handler.HandleAsync(
                new CreateProjectInvitationCommand(
                    ownerId,
                    projectId,
                    recipientEmail,
                    ProjectMemberRole.Viewer));

            Assert.True(result.IsSuccess);
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var statuses = await verificationContext.ProjectInvitations
            .Where(invitation => invitation.ProjectId == projectId
                && invitation.InvitedUserId == recipientId)
            .Select(invitation => invitation.Status)
            .ToListAsync();
        Assert.Equal(2, statuses.Count);
        Assert.Single(statuses, status => status == ProjectInvitationStatus.Expired);
        Assert.Single(statuses, status => status == ProjectInvitationStatus.Pending);
    }

    [Fact]
    public async Task PostgreSql_allows_only_one_concurrent_pending_invitation_per_project_user()
    {
        var ownerId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var projectId = Guid.Empty;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Invitation uniqueness project");
            projectId = project.Id;
            setupContext.Users.AddRange(
                User.Create(
                    EmailAddress.Create($"unique-invite-owner-{ownerId:N}@example.com"),
                    DisplayName.Create("Unique Invite Owner"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: ownerId,
                    createdAt: DateTime.UtcNow),
                User.Create(
                    EmailAddress.Create($"unique-invite-recipient-{recipientId:N}@example.com"),
                    DisplayName.Create("Unique Invite Recipient"),
                    UserRole.User,
                    isActive: true,
                    isEmailConfirmed: true,
                    id: recipientId,
                    createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            await setupContext.SaveChangesAsync();
        }

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        firstContext.ProjectInvitations.Add(CreatePendingInvitation(projectId, ownerId, recipientId));
        secondContext.ProjectInvitations.Add(CreatePendingInvitation(projectId, ownerId, recipientId));

        var saveResults = await Task.WhenAll(
            TrySaveChangesAsync(firstContext),
            TrySaveChangesAsync(secondContext));

        Assert.Single(saveResults, succeeded => succeeded);
        Assert.Single(saveResults, succeeded => !succeeded);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationContext.ProjectInvitations.CountAsync(
            invitation => invitation.ProjectId == projectId
                && invitation.InvitedUserId == recipientId
                && invitation.Status == ProjectInvitationStatus.Pending));
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
            var handler = new AcceptProjectInvitationHandler(
                new EfProjectInvitationResponseStore(responseContext),
                new InvalidProjectInvitationNotificationWriter(responseContext));

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                handler.HandleAsync(new AcceptProjectInvitationCommand(recipientId, token)));
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
            var handler = new AddProjectMemberHandler(
                new EfAddProjectMemberStore(responseContext),
                new FailingAddProjectMemberNotificationWriter());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(new AddProjectMemberCommand(ownerId, projectId, memberId)));
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
            var handler = new RemoveProjectMemberHandler(
                new EfRemoveProjectMemberStore(responseContext),
                new EfProjectTaskMemberAssignmentWriter(responseContext));

            var result = await handler.HandleAsync(
                new RemoveProjectMemberCommand(ownerId, projectId, memberId));

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

    [Fact]
    public async Task PostgreSql_task_deletion_removes_attachment_metadata_and_persists_cleanup_messages()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        var storedFileName = $"task-delete-{Guid.NewGuid():N}.bin";
        var duplicateStoredFileName = $"task-delete-duplicate-{Guid.NewGuid():N}.bin";
        Guid projectId;
        Guid taskId;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Task attachment cleanup project");
            var task = ProjectTask.Create(
                project.Id,
                "Task with attachments",
                null,
                ProjectTaskPriority.Normal,
                null,
                null,
                ownerId);
            projectId = project.Id;
            taskId = task.Id;
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            setupContext.ProjectTaskAttachments.AddRange(
                new ProjectTaskAttachment
                {
                    ProjectTaskId = task.Id,
                    UploadedByUserId = ownerId,
                    OriginalFileName = "first.bin",
                    StoredFileName = storedFileName,
                    ContentType = "application/octet-stream",
                    SizeBytes = 1
                },
                new ProjectTaskAttachment
                {
                    ProjectTaskId = task.Id,
                    UploadedByUserId = ownerId,
                    OriginalFileName = "duplicate.bin",
                    StoredFileName = duplicateStoredFileName,
                    ContentType = "application/octet-stream",
                    SizeBytes = 1
                },
                new ProjectTaskAttachment
                {
                    ProjectTaskId = task.Id,
                    UploadedByUserId = ownerId,
                    OriginalFileName = "duplicate-copy.bin",
                    StoredFileName = duplicateStoredFileName,
                    ContentType = "application/octet-stream",
                    SizeBytes = 1
                });
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new DeleteProjectTaskHandler(
                new EfProjectTaskAccess(responseContext),
                new EfProjectTaskCommandStore(responseContext),
                new EfProjectTaskAttachmentCleanupQueue(responseContext));

            var result = await handler.HandleAsync(new DeleteProjectTaskCommand(
                ownerId,
                projectId,
                taskId,
                await responseContext.ProjectTasks
                    .Where(task => task.Id == taskId)
                    .Select(task => task.ConcurrencyStamp)
                    .SingleAsync()));

            Assert.True(result.IsSuccess);
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verificationContext.ProjectTasks.AnyAsync(task => task.Id == taskId));
        Assert.False(await verificationContext.ProjectTaskAttachments.AnyAsync(
            attachment => attachment.ProjectTaskId == taskId));
        var cleanupMessages = await verificationContext.ProjectTaskAttachmentCleanupMessages
            .Where(message => message.StoredFileName == storedFileName || message.StoredFileName == duplicateStoredFileName)
            .ToListAsync();
        Assert.Equal(2, cleanupMessages.Count);
        Assert.Equal(
            new[] { storedFileName, duplicateStoredFileName }.OrderBy(fileName => fileName),
            cleanupMessages.Select(message => message.StoredFileName).OrderBy(fileName => fileName));
    }

    [Fact]
    public async Task PostgreSql_attachment_store_enforces_count_and_byte_quotas()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        var project = Project.Create(ownerId, "Attachment quota project");
        var task = ProjectTask.Create(
            project.Id,
            "Attachment quota task",
            null,
            ProjectTaskPriority.Normal,
            null,
            null,
            ownerId);

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        var settings = Options.Create(new AttachmentSettings
        {
            MaxFileSizeBytes = 10,
            MaxCountPerTask = 2,
            MaxBytesPerTask = 10
        });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var store = new EfCreateProjectTaskAttachmentStore(
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
                settings);

            await store.CreateAsync(
                CreateAttachmentCommand(ownerId, project.Id, task.Id, 6),
                $"{Guid.NewGuid():N}.txt");

            await Assert.ThrowsAsync<ProjectTaskAttachmentQuotaExceededException>(() => store.CreateAsync(
                CreateAttachmentCommand(ownerId, project.Id, task.Id, 5),
                $"{Guid.NewGuid():N}.txt"));
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationContext.ProjectTaskAttachments.CountAsync(attachment => attachment.ProjectTaskId == task.Id));
    }

    [Fact]
    public async Task PostgreSql_concurrent_attachment_uploads_allow_only_one_when_count_quota_is_one()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        var project = Project.Create(ownerId, "Concurrent attachment quota project");
        var task = ProjectTask.Create(
            project.Id,
            "Concurrent attachment quota task",
            null,
            ProjectTaskPriority.Normal,
            null,
            null,
            ownerId);

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        var settings = Options.Create(new AttachmentSettings
        {
            MaxFileSizeBytes = 10,
            MaxCountPerTask = 1,
            MaxBytesPerTask = 10
        });

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstStore = new EfCreateProjectTaskAttachmentStore(
            firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            settings);
        var secondStore = new EfCreateProjectTaskAttachmentStore(
            secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            settings);

        var results = await Task.WhenAll(
            CaptureAttachmentQuotaResultAsync(firstStore, CreateAttachmentCommand(ownerId, project.Id, task.Id, 4)),
            CaptureAttachmentQuotaResultAsync(secondStore, CreateAttachmentCommand(ownerId, project.Id, task.Id, 4)));

        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is ProjectTaskAttachmentQuotaExceededException);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verificationContext.ProjectTaskAttachments.CountAsync(attachment => attachment.ProjectTaskId == task.Id));
    }

    [Fact]
    public async Task PostgreSql_concurrent_attachment_uploads_allow_only_one_when_byte_quota_would_be_exceeded()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        var project = Project.Create(ownerId, "Concurrent byte quota project");
        var task = ProjectTask.Create(
            project.Id,
            "Concurrent byte quota task",
            null,
            ProjectTaskPriority.Normal,
            null,
            null,
            ownerId);

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            await setupContext.SaveChangesAsync();
        }

        var settings = Options.Create(new AttachmentSettings
        {
            MaxFileSizeBytes = 10,
            MaxCountPerTask = 20,
            MaxBytesPerTask = 5
        });

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstStore = new EfCreateProjectTaskAttachmentStore(
            firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            settings);
        var secondStore = new EfCreateProjectTaskAttachmentStore(
            secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            settings);

        var results = await Task.WhenAll(
            CaptureAttachmentQuotaResultAsync(firstStore, CreateAttachmentCommand(ownerId, project.Id, task.Id, 4)),
            CaptureAttachmentQuotaResultAsync(secondStore, CreateAttachmentCommand(ownerId, project.Id, task.Id, 4)));

        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result is ProjectTaskAttachmentQuotaExceededException);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attachments = await verificationContext.ProjectTaskAttachments
            .Where(attachment => attachment.ProjectTaskId == task.Id)
            .ToListAsync();
        Assert.Single(attachments);
        Assert.Equal(4, attachments[0].SizeBytes);
    }

    private static CreateProjectTaskAttachmentCommand CreateAttachmentCommand(
        Guid userId,
        Guid projectId,
        Guid taskId,
        long sizeBytes)
        => new(
            userId,
            projectId,
            taskId,
            "quota.txt",
            "text/plain",
            sizeBytes,
            new MemoryStream("quota"u8.ToArray()));

    private static async Task<Exception?> CaptureAttachmentQuotaResultAsync(
        EfCreateProjectTaskAttachmentStore store,
        CreateProjectTaskAttachmentCommand command)
    {
        try
        {
            await store.CreateAsync(command, $"{Guid.NewGuid():N}.txt");
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    [Fact]
    public async Task PostgreSql_task_creation_rolls_back_task_and_activity_when_notification_insert_fails()
    {
        var ownerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        Guid projectId;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Task notification rollback project");
            project.AddMember(assigneeId);
            projectId = project.Id;
            setupContext.Users.Add(User.Create(
                EmailAddress.Create($"task-assignment-{assigneeId:N}@example.com"),
                DisplayName.Create("Task Assignment Recipient"),
                UserRole.User,
                isActive: true,
                isEmailConfirmed: true,
                id: assigneeId,
                createdAt: DateTime.UtcNow));
            setupContext.Projects.Add(project);
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var handler = new CreateProjectTaskHandler(
                new EfProjectTaskAccess(responseContext),
                new EfProjectTaskCommandStore(responseContext),
                new InvalidAssignmentNotificationWriter(responseContext));

            await Assert.ThrowsAsync<DbUpdateException>(() => handler.HandleAsync(
                new CreateProjectTaskCommand(
                    ownerId,
                    projectId,
                    "Task should roll back",
                    null,
                    ProjectTaskPriority.Normal,
                    null,
                    assigneeId,
                    [])));
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verificationContext.ProjectTasks.AnyAsync(task => task.ProjectId == projectId));
        Assert.False(await verificationContext.ProjectActivities.AnyAsync(activity => activity.ProjectId == projectId));
        Assert.False(await verificationContext.Notifications.AnyAsync(notification => notification.ProjectId == projectId));
    }

    [Fact]
    public async Task PostgreSql_task_deletion_rolls_back_cleanup_and_metadata_when_cleanup_insert_fails()
    {
        var ownerId = Guid.NewGuid();
        await SeedProjectOwnerAsync(ownerId);
        var storedFileName = $"task-delete-rollback-{Guid.NewGuid():N}.bin";
        Guid projectId;
        Guid taskId;

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var setupContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = Project.Create(ownerId, "Task cleanup rollback project");
            var task = ProjectTask.Create(
                project.Id,
                "Task cleanup rollback",
                null,
                ProjectTaskPriority.Normal,
                null,
                null,
                ownerId);
            projectId = project.Id;
            taskId = task.Id;
            setupContext.Projects.Add(project);
            setupContext.ProjectTasks.Add(task);
            setupContext.ProjectTaskAttachments.Add(new ProjectTaskAttachment
            {
                ProjectTaskId = task.Id,
                UploadedByUserId = ownerId,
                OriginalFileName = "rollback.bin",
                StoredFileName = storedFileName,
                ContentType = "application/octet-stream",
                SizeBytes = 1
            });
            await setupContext.SaveChangesAsync();
        }

        await using (var responseScope = _factory.Services.CreateAsyncScope())
        {
            var responseContext = responseScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cleanupQueue = new FailingCleanupQueue(
                new EfProjectTaskAttachmentCleanupQueue(responseContext));
            var handler = new DeleteProjectTaskHandler(
                new EfProjectTaskAccess(responseContext),
                new EfProjectTaskCommandStore(responseContext),
                cleanupQueue);
            var concurrencyStamp = await responseContext.ProjectTasks
                .Where(task => task.Id == taskId)
                .Select(task => task.ConcurrencyStamp)
                .SingleAsync();

            await Assert.ThrowsAsync<DbUpdateException>(() => handler.HandleAsync(
                new DeleteProjectTaskCommand(
                    ownerId,
                    projectId,
                    taskId,
                    concurrencyStamp)));
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await verificationContext.ProjectTasks.AnyAsync(task => task.Id == taskId));
        Assert.True(await verificationContext.ProjectTaskAttachments.AnyAsync(
            attachment => attachment.ProjectTaskId == taskId));
        Assert.False(await verificationContext.ProjectTaskAttachmentCleanupMessages.AnyAsync(
            message => message.StoredFileName == storedFileName));
        Assert.False(await verificationContext.ProjectTaskAttachmentCleanupMessages.AnyAsync(
            message => message.StoredFileName == new string('x', 101)));
    }

    private static ProjectInvitation CreatePendingInvitation(
        Guid projectId,
        Guid ownerId,
        Guid recipientId)
        => new()
        {
            ProjectId = projectId,
            InvitedUserId = recipientId,
            InvitedByUserId = ownerId,
            Role = ProjectMemberRole.Member,
            TokenHash = HashToken(Guid.NewGuid().ToString("N")),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

    private static async Task<bool> TrySaveChangesAsync(ApplicationDbContext dbContext)
    {
        try
        {
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private sealed class FailingCleanupQueue : IProjectTaskAttachmentCleanupQueue
    {
        private readonly IProjectTaskAttachmentCleanupQueue _inner;

        public FailingCleanupQueue(IProjectTaskAttachmentCleanupQueue inner)
        {
            _inner = inner;
        }

        public Task<IReadOnlyList<string>> PrepareTaskDeletionAsync(
            Guid projectTaskId,
            CancellationToken cancellationToken = default)
            => _inner.PrepareTaskDeletionAsync(projectTaskId, cancellationToken);

        public void Enqueue(string storedFileName)
            => _inner.Enqueue(new string('x', 101));
    }

    private sealed class InvalidAssignmentNotificationWriter : IProjectTaskAssignmentNotificationWriter
    {
        private readonly ApplicationDbContext _dbContext;

        public InvalidAssignmentNotificationWriter(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task AddTaskAssignedNotificationAsync(
            Guid assigneeUserId,
            Guid projectId,
            Guid projectTaskId,
            string taskTitle,
            CancellationToken cancellationToken = default)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Type = NotificationType.TaskAssigned,
                Title = "Invalid test notification",
                Message = "This notification intentionally violates the user foreign key.",
                ResourceType = "ProjectTask",
                ResourceId = projectTaskId,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }
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

    private sealed class InvalidProjectInvitationNotificationWriter : IProjectInvitationNotificationWriter
    {
        private readonly ApplicationDbContext _dbContext;

        public InvalidProjectInvitationNotificationWriter(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task AddInvitationCreatedNotificationAsync(
            Guid recipientUserId,
            Guid projectId,
            Guid invitationId,
            string projectName,
            string inviterDisplayName,
            CancellationToken cancellationToken = default)
            => AddInvalidNotificationAsync(projectId, invitationId);

        public Task AddInvitationResponseNotificationAsync(
            Guid ownerUserId,
            Guid projectId,
            Guid invitationId,
            string projectName,
            string recipientDisplayName,
            ProjectInvitationStatus status,
            CancellationToken cancellationToken = default)
            => AddInvalidNotificationAsync(projectId, invitationId);

        private Task AddInvalidNotificationAsync(Guid projectId, Guid invitationId)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Type = NotificationType.ProjectInvitation,
                Title = "Invalid test notification",
                Message = "This notification intentionally violates the user foreign key.",
                ResourceType = "ProjectInvitation",
                ResourceId = invitationId,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }
    }

    private sealed class FailingAddProjectMemberNotificationWriter : IAddProjectMemberNotificationWriter
    {
        public Task AddProjectMemberNotificationAsync(
            Guid userId,
            Guid projectId,
            string projectName,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Notification persistence failed.");
    }
}