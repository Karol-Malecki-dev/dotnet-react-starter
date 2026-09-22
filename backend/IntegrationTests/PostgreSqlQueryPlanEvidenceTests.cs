using System.Data.Common;
using System.Text;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlQueryPlanEvidenceTests
{
    private const int BenchmarkTaskCount = 1_000;
    private const int NoiseProjectCount = 20;
    private readonly PostgreSqlWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public PostgreSqlQueryPlanEvidenceTests(
        PostgreSqlWebApplicationFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task PostgreSql_query_plan_evidence_is_captured_for_v6_fixture()
    {
        var fixture = await SeedFixtureAsync();
        var report = new StringBuilder();
        var today = DateTime.UtcNow.Date;
        var nextDay = today.AddDays(8);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();

        try
        {
            report.AppendLine("# V6 PostgreSQL query-plan evidence");
            report.AppendLine();
            report.AppendLine($"- Generated UTC: {DateTime.UtcNow:O}");
            report.AppendLine($"- Application version: {Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "integration-test"}");
            report.AppendLine($"- Benchmark project ID: {fixture.ProjectId}");
            report.AppendLine($"- Benchmark owner ID: {fixture.OwnerId}");
            report.AppendLine($"- Fixture: {NoiseProjectCount} noise projects; {BenchmarkTaskCount} benchmark tasks; labels and activity included");
            report.AppendLine($"- Dashboard date window: {today:yyyy-MM-dd} through {nextDay:yyyy-MM-dd} (exclusive)");
            report.AppendLine();
            report.AppendLine("> Plans were captured with EXPLAIN (ANALYZE, BUFFERS, VERBOSE) against a disposable Testcontainers PostgreSQL database.");
            report.AppendLine("> This report is diagnostic evidence; it does not force a planner choice or change application schema.");
            report.AppendLine();

            AppendSection(report, "Database metadata");
            report.AppendLine("```text");
            report.AppendLine(await ReadTextAsync(
                dbContext.Database.GetDbConnection(),
                """
                SELECT version(),
                       current_database(),
                       current_schema(),
                       pg_size_pretty(pg_database_size(current_database()));
                """));
            report.AppendLine("```");
            report.AppendLine();

            AppendSection(report, "Fixture row counts");
            report.AppendLine("```text");
            report.AppendLine(await ReadTextAsync(
                dbContext.Database.GetDbConnection(),
                """
                SELECT
                    (SELECT COUNT(*) FROM "Users") AS "Users",
                    (SELECT COUNT(*) FROM "Projects") AS "Projects",
                    (SELECT COUNT(*) FROM "ProjectTasks") AS "ProjectTasks",
                    (SELECT COUNT(*) FROM "ProjectTaskLabels") AS "ProjectTaskLabels",
                    (SELECT COUNT(*) FROM "ProjectActivities") AS "ProjectActivities",
                    (SELECT COUNT(*) FROM "ProjectTasks" WHERE "ProjectId" = @projectId) AS "SelectedProjectTasks",
                    (SELECT COUNT(*) FROM "ProjectActivities" WHERE "ProjectId" = @projectId) AS "SelectedProjectActivities";
                """,
                ("projectId", fixture.ProjectId)));
            report.AppendLine("```");
            report.AppendLine();

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Visible project list",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT project.*
                FROM "Projects" AS project
                WHERE (
                    project."OwnerId" = @userId
                    OR EXISTS (
                        SELECT 1
                        FROM "ProjectMembers" AS member
                        INNER JOIN "Users" AS member_user ON member_user."Id" = member."UserId"
                        WHERE member."ProjectId" = project."Id"
                          AND member."UserId" = @userId
                          AND member_user."IsActive"
                    )
                )
                  AND NOT project."IsArchived"
                ORDER BY project."UpdatedAt" DESC;
                """,
                ("userId", fixture.OwnerId));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Project task count",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT COUNT(*)
                FROM "ProjectTasks"
                WHERE "ProjectId" = @projectId;
                """,
                ("projectId", fixture.ProjectId));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Project task page with labels",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT task.*, label.*
                FROM (
                    SELECT task.*
                    FROM "ProjectTasks" AS task
                    WHERE task."ProjectId" = @projectId
                    ORDER BY task."CreatedAt" DESC
                    LIMIT 20
                ) AS task
                LEFT JOIN "ProjectTaskLabels" AS label
                    ON task."Id" = label."ProjectTaskId"
                ORDER BY task."CreatedAt" DESC, task."Id", label."Id";
                """,
                ("projectId", fixture.ProjectId));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Project dashboard task statistics",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT
                    COUNT(*) AS "Total",
                    COUNT(*) FILTER (WHERE "Status" = 'Todo') AS "Todo",
                    COUNT(*) FILTER (WHERE "Status" = 'InProgress') AS "InProgress",
                    COUNT(*) FILTER (WHERE "Status" = 'Done') AS "Done",
                    COUNT(*) FILTER (WHERE "Priority" = 'Low') AS "LowPriority",
                    COUNT(*) FILTER (WHERE "Priority" = 'Normal') AS "NormalPriority",
                    COUNT(*) FILTER (WHERE "Priority" = 'High') AS "HighPriority"
                FROM "ProjectTasks"
                WHERE "ProjectId" = @projectId;
                """,
                ("projectId", fixture.ProjectId));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Project dashboard overdue tasks",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT task.*, label.*
                FROM (
                    SELECT task.*
                    FROM "ProjectTasks" AS task
                    WHERE task."ProjectId" = @projectId
                      AND task."DueDate" IS NOT NULL
                      AND task."DueDate" < @today
                      AND task."Status" <> 'Done'
                    ORDER BY task."DueDate"
                    LIMIT 10
                ) AS task
                LEFT JOIN "ProjectTaskLabels" AS label
                    ON task."Id" = label."ProjectTaskId"
                ORDER BY task."DueDate", task."Id", label."Id";
                """,
                ("projectId", fixture.ProjectId),
                ("today", today));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Project dashboard upcoming tasks",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT task.*, label.*
                FROM (
                    SELECT task.*
                    FROM "ProjectTasks" AS task
                    WHERE task."ProjectId" = @projectId
                      AND task."DueDate" IS NOT NULL
                      AND task."DueDate" >= @today
                      AND task."DueDate" < @nextDay
                      AND task."Status" <> 'Done'
                    ORDER BY task."DueDate"
                    LIMIT 10
                ) AS task
                LEFT JOIN "ProjectTaskLabels" AS label
                    ON task."Id" = label."ProjectTaskId"
                ORDER BY task."DueDate", task."Id", label."Id";
                """,
                ("projectId", fixture.ProjectId),
                ("today", today),
                ("nextDay", nextDay));

            await AppendPlanAsync(
                report,
                dbContext.Database.GetDbConnection(),
                "Recent project activity",
                """
                EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
                SELECT
                    activity."Id",
                    activity."Type",
                    activity."Description",
                    activity."ActorUserId",
                    activity."ProjectTaskId",
                    activity."CreatedAt",
                    actor."DisplayName"
                FROM "ProjectActivities" AS activity
                INNER JOIN "Users" AS actor
                    ON activity."ActorUserId" = actor."Id"
                WHERE activity."ProjectId" = @projectId
                ORDER BY activity."CreatedAt" DESC
                LIMIT 5;
                """,
                ("projectId", fixture.ProjectId));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        var reportPath = await WriteReportAsync(report);
        _output.WriteLine($"V6 PostgreSQL query-plan report: {reportPath}");

        Assert.Contains("Execution Time", report.ToString(), StringComparison.Ordinal);
        Assert.Contains("Planning Time", report.ToString(), StringComparison.Ordinal);
    }

    private async Task<(Guid OwnerId, Guid ProjectId)> SeedFixtureAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ownerId = Guid.NewGuid();
        var owner = User.Create(
            EmailAddress.Create($"v6-query-plan-owner-{ownerId:N}@example.com"),
            DisplayName.Create("V6 Query Plan Owner"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true,
            id: ownerId,
            createdAt: DateTime.UtcNow);
        var benchmarkProject = Project.Create(ownerId, $"V6 benchmark {Guid.NewGuid():N}");
        var noiseProjects = Enumerable
            .Range(0, NoiseProjectCount)
            .Select(index => Project.Create(ownerId, $"V6 noise project {index} {Guid.NewGuid():N}"))
            .ToList();

        dbContext.Users.Add(owner);
        dbContext.Projects.Add(benchmarkProject);
        dbContext.Projects.AddRange(noiseProjects);

        var tasks = new List<ProjectTask>(BenchmarkTaskCount);
        for (var index = 0; index < BenchmarkTaskCount; index++)
        {
            var dueDate = (index % 5) switch
            {
                0 => (DateTime?)DateTime.UtcNow.Date.AddDays(-index % 31),
                1 => (DateTime?)DateTime.UtcNow.Date.AddDays(index % 8),
                _ => (DateTime?)null
            };
            var task = ProjectTask.Create(
                benchmarkProject.Id,
                $"V6 benchmark task {index:D4}",
                "Synthetic V6 query-plan fixture task.",
                (ProjectTaskPriority)(index % 3 + 1),
                dueDate,
                assignedUserId: null,
                createdByUserId: ownerId,
                labels: new[] { $"bucket-{index % 10}", index % 2 == 0 ? "even" : "odd" });

            if (index % 7 == 0)
            {
                task.ChangeStatus(ProjectTaskStatus.Done);
            }
            else if (index % 3 == 0)
            {
                task.ChangeStatus(ProjectTaskStatus.InProgress);
            }

            tasks.Add(task);
        }

        dbContext.ProjectTasks.AddRange(tasks);
        dbContext.ProjectActivities.AddRange(
            tasks
                .Where((_, index) => index % 5 == 0)
                .Select(task => new ProjectActivity
                {
                    ProjectId = benchmarkProject.Id,
                    ActorUserId = ownerId,
                    ProjectTaskId = task.Id,
                    Type = "TaskUpdated",
                    Description = "Synthetic V6 query-plan fixture activity.",
                    CreatedAt = DateTime.UtcNow
                }));

        await dbContext.SaveChangesAsync();
        return (ownerId, benchmarkProject.Id);
    }

    private static async Task AppendPlanAsync(
        StringBuilder report,
        DbConnection connection,
        string name,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        AppendSection(report, name);
        report.AppendLine("```sql");
        report.AppendLine(sql.Trim());
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("```text");
        report.AppendLine(await ReadTextAsync(connection, sql, parameters));
        report.AppendLine("```");
        report.AppendLine();
    }

    private static async Task<string> ReadTextAsync(
        DbConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        var lines = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.GetValue(index)?.ToString() ?? string.Empty;
            }

            lines.Add(string.Join('\t', values));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendSection(StringBuilder report, string name)
    {
        report.AppendLine($"## {name}");
        report.AppendLine();
    }

    private static async Task<string> WriteReportAsync(StringBuilder report)
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(
            repositoryRoot,
            "artifacts",
            "v6");
        Directory.CreateDirectory(outputDirectory);

        var path = Path.Combine(
            outputDirectory,
            $"query-plan-evidence-{DateTime.UtcNow:yyyyMMdd-HHmmssZ}.md");
        await File.WriteAllTextAsync(path, report.ToString());
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "backend.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetTempPath();
    }
}
