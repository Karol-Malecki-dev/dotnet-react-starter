using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Application.Interfaces;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlLargeFixtureApiBaselineTests
{
    private const int DefaultTaskCount = 10_000;
    private const int DefaultNoiseProjectCount = 20;
    private const int DefaultWarmupRequests = 5;
    private const int DefaultMeasuredRequests = 30;
    private readonly PostgreSqlWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public PostgreSqlLargeFixtureApiBaselineTests(
        PostgreSqlWebApplicationFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [V6LargeFixtureFact]
    [Trait("Category", "Performance")]
    public async Task PostgreSql_large_fixture_api_baseline_is_captured()
    {
        var taskCount = ReadPositiveInt("V6_LARGE_FIXTURE_TASK_COUNT", DefaultTaskCount);
        var noiseProjectCount = ReadNonNegativeInt(
            "V6_LARGE_FIXTURE_NOISE_PROJECT_COUNT",
            DefaultNoiseProjectCount);
        var warmupRequests = ReadNonNegativeInt("V6_LARGE_FIXTURE_WARMUP_REQUESTS", DefaultWarmupRequests);
        var measuredRequests = ReadPositiveInt(
            "V6_LARGE_FIXTURE_MEASURED_REQUESTS",
            DefaultMeasuredRequests);

        V6BenchmarkFixture? fixture = null;
        try
        {
            fixture = await SeedFixtureAsync(taskCount, noiseProjectCount);
            var accessToken = await GenerateAccessTokenAsync(fixture.OwnerId);
            using var client = _factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-react-starter-v6-large-fixture/1.0");

            var scenarios = new[]
            {
                new ApiScenario(
                    "projects.list",
                    "/api/projects?scope=all",
                    "Visible project projection and membership visibility query"),
                new ApiScenario(
                    "project.tasks.page-1",
                    $"/api/projects/{fixture.ProjectId}/tasks?pageNumber=1&pageSize=20&sortBy=createdAt&sortDirection=descending",
                    "Count plus ordered, paged task query with label projection"),
                new ApiScenario(
                    "project.dashboard",
                    $"/api/projects/{fixture.ProjectId}/dashboard",
                    "Composed task metrics, due dates, and recent activity read")
            };

            var results = new List<ApiScenarioResult>(scenarios.Length);
            foreach (var scenario in scenarios)
            {
                results.Add(
                    await MeasureScenarioAsync(
                        client,
                        scenario,
                        warmupRequests,
                        measuredRequests));
            }

            var plans = await CapturePlansAsync(fixture);
            var report = BuildReport(
                fixture,
                taskCount,
                noiseProjectCount,
                warmupRequests,
                measuredRequests,
                results,
                plans);
            var reportPath = await WriteReportAsync(report);
            _output.WriteLine($"V6 large-fixture API baseline report: {reportPath}");

            Assert.All(
                results,
                result => Assert.True(
                    result.Failed == 0,
                    $"{result.Scenario.Name} failed requests: {string.Join("; ", result.Errors)}"));
        }
        finally
        {
            if (fixture is not null)
            {
                await DeleteFixtureAsync(fixture);
            }
        }
    }

    private async Task<V6BenchmarkFixture> SeedFixtureAsync(
        int taskCount,
        int noiseProjectCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await V6BenchmarkFixtureSeeder.SeedAsync(
            dbContext,
            taskCount,
            noiseProjectCount);
    }

    private async Task<string> GenerateAccessTokenAsync(Guid ownerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var owner = await dbContext.Users.SingleAsync(user => user.Id == ownerId);
        var tokens = await tokenService.GenerateTokensAsync(owner);
        return tokens.AccessToken;
    }

    private async Task DeleteFixtureAsync(V6BenchmarkFixture fixture)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Projects
            .Where(project => project.Id == fixture.ProjectId)
            .ExecuteDeleteAsync();
        await dbContext.Users
            .Where(user => user.Id == fixture.OwnerId)
            .ExecuteDeleteAsync();
    }

    private static async Task<ApiScenarioResult> MeasureScenarioAsync(
        HttpClient client,
        ApiScenario scenario,
        int warmupRequests,
        int measuredRequests)
    {
        for (var index = 0; index < warmupRequests; index++)
        {
            var warmup = await SendRequestAsync(client, scenario.Path);
            if (!warmup.Success)
            {
                throw new InvalidOperationException(
                    $"Warmup failed for {scenario.Name} with HTTP {warmup.StatusCode}: {warmup.Error}");
            }
        }

        var measurements = new List<ApiRequestMeasurement>(measuredRequests);
        var wallClock = Stopwatch.StartNew();
        for (var index = 0; index < measuredRequests; index++)
        {
            measurements.Add(await SendRequestAsync(client, scenario.Path));
        }

        wallClock.Stop();
        var successfulMeasurements = measurements
            .Where(measurement => measurement.Success)
            .ToArray();
        var latencies = successfulMeasurements
            .Select(measurement => measurement.LatencyMs)
            .ToArray();
        var payloadSizes = successfulMeasurements
            .Select(measurement => measurement.PayloadBytes)
            .ToArray();
        var elapsedSeconds = wallClock.Elapsed.TotalSeconds;

        return new ApiScenarioResult(
            scenario,
            measurements.Count,
            successfulMeasurements.Length,
            measurements.Count - successfulMeasurements.Length,
            measurements.Count == 0
                ? 0
                : (double)(measurements.Count - successfulMeasurements.Length) / measurements.Count * 100,
            Percentile(latencies, 50),
            Percentile(latencies, 95),
            Percentile(latencies, 99),
            latencies.Length == 0 ? null : latencies.Average(),
            payloadSizes.Length == 0 ? null : payloadSizes.Average(),
            elapsedSeconds <= 0 ? null : measurements.Count / elapsedSeconds,
            measurements
                .GroupBy(measurement => measurement.StatusCode)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}: {group.Count()}")
                .ToArray(),
            measurements
                .Where(measurement => !measurement.Success && measurement.Error is not null)
                .Select(measurement => measurement.Error!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static async Task<ApiRequestMeasurement> SendRequestAsync(
        HttpClient client,
        string path)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(
                path,
                HttpCompletionOption.ResponseHeadersRead);
            var body = await response.Content.ReadAsByteArrayAsync();
            stopwatch.Stop();
            var success = (int)response.StatusCode is >= 200 and < 300;
            return new ApiRequestMeasurement(
                success,
                (int)response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                body.Length,
                success ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            return new ApiRequestMeasurement(
                false,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                exception.Message);
        }
        catch (TaskCanceledException exception)
        {
            stopwatch.Stop();
            return new ApiRequestMeasurement(
                false,
                0,
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                exception.Message);
        }
    }

    private async Task<IReadOnlyList<PlanEvidence>> CapturePlansAsync(
        V6BenchmarkFixture fixture)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();
        var today = DateTime.UtcNow.Date;
        var nextDay = today.AddDays(8);

        try
        {
            return
            [
                await CapturePlanAsync(
                    connection,
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
                    ("projectId", fixture.ProjectId)),
                await CapturePlanAsync(
                    connection,
                    "Project dashboard statistics",
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
                    ("projectId", fixture.ProjectId)),
                await CapturePlanAsync(
                    connection,
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
                    ("today", today)),
                await CapturePlanAsync(
                    connection,
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
                    ("nextDay", nextDay)),
                await CapturePlanAsync(
                    connection,
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
                    ("projectId", fixture.ProjectId))
            ];
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<PlanEvidence> CapturePlanAsync(
        DbConnection connection,
        string name,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        return new PlanEvidence(
            name,
            sql.Trim(),
            await ReadTextAsync(connection, sql, parameters));
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

    private static StringBuilder BuildReport(
        V6BenchmarkFixture fixture,
        int taskCount,
        int noiseProjectCount,
        int warmupRequests,
        int measuredRequests,
        IReadOnlyList<ApiScenarioResult> results,
        IReadOnlyList<PlanEvidence> plans)
    {
        var report = new StringBuilder();
        report.AppendLine("# V6 large-fixture API baseline");
        report.AppendLine();
        report.AppendLine($"- Generated UTC: {DateTime.UtcNow:O}");
        report.AppendLine($"- Application version: {Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "integration-test"}");
        report.AppendLine($"- Benchmark project ID: {fixture.ProjectId}");
        report.AppendLine($"- Benchmark owner ID: {fixture.OwnerId}");
        report.AppendLine($"- Fixture: {noiseProjectCount} noise projects; {taskCount} benchmark tasks; {fixture.LabelCount} labels; {fixture.ActivityCount} activities");
        report.AppendLine($"- Warmup requests per scenario: {warmupRequests}");
        report.AppendLine($"- Measured requests per scenario: {measuredRequests}");
        report.AppendLine("- Concurrency: 1 (sequential closed-loop measurement)");
        report.AppendLine("- Database: disposable Testcontainers PostgreSQL 16");
        report.AppendLine();
        report.AppendLine("> This report combines API latency measurements with PostgreSQL plan evidence for the same fixture. It is a repeatable application baseline, not a capacity or stress test.");
        report.AppendLine();
        report.AppendLine("## API results");
        report.AppendLine();
        report.AppendLine("| Scenario | Endpoint | Purpose | Requests | Success | Errors | Error rate | p50 ms | p95 ms | p99 ms | Avg ms | Avg payload bytes | Throughput req/s |");
        report.AppendLine("|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var result in results)
        {
            report.AppendLine(
                $"| {result.Scenario.Name} | `{result.Scenario.Path}` | {result.Scenario.Purpose} | {result.Requested} | {result.Successful} | {result.Failed} | {Format(result.ErrorRate)}% | {Format(result.P50)} | {Format(result.P95)} | {Format(result.P99)} | {Format(result.AverageLatency)} | {Format(result.AveragePayload)} | {Format(result.Throughput)} |");
        }

        report.AppendLine();
        report.AppendLine("## HTTP status distribution");
        report.AppendLine();
        foreach (var result in results)
        {
            report.AppendLine($"- **{result.Scenario.Name}:** {string.Join(", ", result.StatusCounts)}");
            if (result.Errors.Count > 0)
            {
                report.AppendLine($"  - Errors: {string.Join("; ", result.Errors)}");
            }
        }

        report.AppendLine();
        report.AppendLine("## PostgreSQL plans");
        report.AppendLine();
        foreach (var plan in plans)
        {
            report.AppendLine($"### {plan.Name}");
            report.AppendLine();
            report.AppendLine("```sql");
            report.AppendLine(plan.Sql);
            report.AppendLine("```");
            report.AppendLine();
            report.AppendLine("```text");
            report.AppendLine(plan.Output);
            report.AppendLine("```");
            report.AppendLine();
        }

        report.AppendLine("## Interpretation notes");
        report.AppendLine();
        report.AppendLine("- Latency percentiles use the nearest-rank value over successful measured requests only.");
        report.AppendLine("- Error rate includes non-2xx responses and transport exceptions.");
        report.AppendLine("- Throughput is measured requests divided by wall-clock duration for the sequential run.");
        report.AppendLine("- Compare reports only when the application version, database engine, fixture size, request parameters, and host resources are recorded.");
        return report;
    }

    private static double? Percentile(IReadOnlyList<double> values, int percentile)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var rank = (int)Math.Ceiling(percentile / 100d * ordered.Length);
        return ordered[Math.Max(rank - 1, 0)];
    }

    private static string Format(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "n/a";

    private static int ReadPositiveInt(string variableName, int defaultValue)
    {
        var value = ReadInt(variableName, defaultValue);
        if (value < 1)
        {
            throw new InvalidOperationException($"{variableName} must be greater than zero.");
        }

        return value;
    }

    private static int ReadNonNegativeInt(string variableName, int defaultValue)
    {
        var value = ReadInt(variableName, defaultValue);
        if (value < 0)
        {
            throw new InvalidOperationException($"{variableName} cannot be negative.");
        }

        return value;
    }

    private static int ReadInt(string variableName, int defaultValue)
    {
        var rawValue = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(rawValue)
            ? defaultValue
            : int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new InvalidOperationException($"{variableName} must be an invariant integer.");
    }

    private async Task<string> WriteReportAsync(StringBuilder report)
    {
        var configuredOutputDirectory = Environment.GetEnvironmentVariable("V6_BASELINE_OUTPUT_DIRECTORY");
        var outputDirectory = string.IsNullOrWhiteSpace(configuredOutputDirectory)
            ? Path.Combine(FindRepositoryRoot(), "artifacts", "v6")
            : Path.GetFullPath(configuredOutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var path = Path.Combine(
            outputDirectory,
            $"large-fixture-api-baseline-{DateTime.UtcNow:yyyyMMdd-HHmmssZ}.md");
        await File.WriteAllTextAsync(path, report.ToString());
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var startDirectories = new[]
        {
            new DirectoryInfo(Directory.GetCurrentDirectory()),
            new DirectoryInfo(AppContext.BaseDirectory)
        };

        foreach (var startDirectory in startDirectories)
        {
            var directory = startDirectory;
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "backend", "backend.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Path.GetTempPath();
    }

    private sealed record ApiScenario(
        string Name,
        string Path,
        string Purpose);

    private sealed record ApiRequestMeasurement(
        bool Success,
        int StatusCode,
        double LatencyMs,
        int PayloadBytes,
        string? Error);

    private sealed record ApiScenarioResult(
        ApiScenario Scenario,
        int Requested,
        int Successful,
        int Failed,
        double ErrorRate,
        double? P50,
        double? P95,
        double? P99,
        double? AverageLatency,
        double? AveragePayload,
        double? Throughput,
        IReadOnlyList<string> StatusCounts,
        IReadOnlyList<string> Errors);

    private sealed record PlanEvidence(
        string Name,
        string Sql,
        string Output);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class V6LargeFixtureFactAttribute : FactAttribute
{
    public V6LargeFixtureFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("V6_RUN_LARGE_FIXTURE_BASELINE"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set V6_RUN_LARGE_FIXTURE_BASELINE=true to run the opt-in large-fixture baseline.";
        }
    }
}
