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
public sealed class PostgreSqlTaskPaginationCandidateTests
{
    private const int DefaultTaskCount = 10_000;
    private const int DefaultWarmupRequests = 5;
    private const int DefaultMeasuredRequests = 30;
    private const string CandidateIndexName = "IX_ProjectTasks_ProjectId_CreatedAt";
    private readonly PostgreSqlWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public PostgreSqlTaskPaginationCandidateTests(
        PostgreSqlWebApplicationFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [V6CandidateExperimentFact]
    [Trait("Category", "Performance")]
    public async Task Task_pagination_candidate_index_is_compared_before_and_after()
    {
        var taskCount = ReadPositiveInt("V6_CANDIDATE_TASK_COUNT", DefaultTaskCount);
        var warmupRequests = ReadNonNegativeInt(
            "V6_CANDIDATE_WARMUP_REQUESTS",
            DefaultWarmupRequests);
        var measuredRequests = ReadPositiveInt(
            "V6_CANDIDATE_MEASURED_REQUESTS",
            DefaultMeasuredRequests);

        V6BenchmarkFixture? fixture = null;
        try
        {
            fixture = await SeedFixtureAsync(taskCount);
            var accessToken = await GenerateAccessTokenAsync(fixture.OwnerId);
            using var client = _factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-react-starter-v6-index-candidate/1.0");

            var path =
                $"/api/projects/{fixture.ProjectId}/tasks?pageNumber=1&pageSize=20&sortBy=createdAt&sortDirection=descending";
            await DropCandidateIndexAsync();
            await AnalyzeFixtureTablesAsync();
            var before = await MeasureAsync(client, path, warmupRequests, measuredRequests);
            var beforePlan = await CapturePlanAsync(fixture.ProjectId);

            await CreateCandidateIndexAsync();

            var after = await MeasureAsync(client, path, warmupRequests, measuredRequests);
            var afterPlan = await CapturePlanAsync(fixture.ProjectId);
            var report = BuildReport(
                fixture,
                taskCount,
                warmupRequests,
                measuredRequests,
                before,
                beforePlan,
                after,
                afterPlan);
            var reportPath = await WriteReportAsync(report);
            _output.WriteLine($"V6 task-pagination candidate report: {reportPath}");

            Assert.Equal(0, before.Failed);
            Assert.Equal(0, after.Failed);
            Assert.Contains(CandidateIndexName, afterPlan, StringComparison.Ordinal);
        }
        finally
        {
            if (fixture is not null)
            {
                await DeleteFixtureAsync(fixture);
            }
        }
    }

    private async Task<V6BenchmarkFixture> SeedFixtureAsync(int taskCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await V6BenchmarkFixtureSeeder.SeedAsync(dbContext, taskCount);
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

    private async Task CreateCandidateIndexAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                $"""CREATE INDEX "{CandidateIndexName}" ON "ProjectTasks" ("ProjectId", "CreatedAt" DESC);""");
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                """ANALYZE "ProjectTasks";""");
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                """ANALYZE "ProjectTaskLabels";""");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task DropCandidateIndexAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                $"""DROP INDEX IF EXISTS "{CandidateIndexName}";""");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task AnalyzeFixtureTablesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                """ANALYZE "ProjectTasks";""");
            await ExecuteNonQueryAsync(
                dbContext.Database.GetDbConnection(),
                """ANALYZE "ProjectTaskLabels";""");
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<string> CapturePlanAsync(Guid projectId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();
        try
        {
            return await ReadTextAsync(
                dbContext.Database.GetDbConnection(),
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
                ("projectId", projectId));
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<ApiMeasurement> MeasureAsync(
        HttpClient client,
        string path,
        int warmupRequests,
        int measuredRequests)
    {
        for (var index = 0; index < warmupRequests; index++)
        {
            using var warmupResponse = await client.GetAsync(path);
            if (!warmupResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Warmup failed with HTTP {(int)warmupResponse.StatusCode}.");
            }
        }

        var measurements = new List<RequestMeasurement>(measuredRequests);
        for (var index = 0; index < measuredRequests; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(
                    path,
                    HttpCompletionOption.ResponseHeadersRead);
                await response.Content.ReadAsByteArrayAsync();
                stopwatch.Stop();
                measurements.Add(
                    new RequestMeasurement(
                        (int)response.StatusCode is >= 200 and < 300,
                        stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (HttpRequestException)
            {
                stopwatch.Stop();
                measurements.Add(new RequestMeasurement(false, stopwatch.Elapsed.TotalMilliseconds));
            }
            catch (TaskCanceledException)
            {
                stopwatch.Stop();
                measurements.Add(new RequestMeasurement(false, stopwatch.Elapsed.TotalMilliseconds));
            }
        }

        var successful = measurements
            .Where(measurement => measurement.Success)
            .Select(measurement => measurement.LatencyMs)
            .ToArray();
        return new ApiMeasurement(
            measurements.Count,
            measurements.Count - successful.Length,
            Percentile(successful, 50),
            Percentile(successful, 95),
            Percentile(successful, 99),
            successful.Length == 0 ? null : successful.Average());
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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

            lines.Add(string.Join(Environment.NewLine, values));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static StringBuilder BuildReport(
        V6BenchmarkFixture fixture,
        int taskCount,
        int warmupRequests,
        int measuredRequests,
        ApiMeasurement before,
        string beforePlan,
        ApiMeasurement after,
        string afterPlan)
    {
        var report = new StringBuilder();
        report.AppendLine("# V6 task-pagination index candidate");
        report.AppendLine();
        report.AppendLine($"- Generated UTC: {DateTime.UtcNow:O}");
        report.AppendLine($"- Benchmark project ID: {fixture.ProjectId}");
        report.AppendLine($"- Fixture tasks: {taskCount}");
        report.AppendLine($"- Warmup requests per phase: {warmupRequests}");
        report.AppendLine($"- Measured requests per phase: {measuredRequests}");
        report.AppendLine($"- Candidate: `{CandidateIndexName}` on `ProjectId`, `CreatedAt DESC`");
        report.AppendLine("- Scope: disposable Testcontainers PostgreSQL only; the migrated index is dropped for the before phase and recreated for the after phase.");
        report.AppendLine("- The experiment did not alter repository migrations; the production migration is validated separately.");
        report.AppendLine();
        report.AppendLine("| Phase | Requests | Errors | p50 ms | p95 ms | p99 ms | Average ms |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        AppendMeasurement(report, "Before candidate index", before);
        AppendMeasurement(report, "After candidate index", after);
        report.AppendLine();
        report.AppendLine("## Before plan");
        report.AppendLine();
        report.AppendLine("```text");
        report.AppendLine(beforePlan);
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("## After plan");
        report.AppendLine();
        report.AppendLine("```text");
        report.AppendLine(afterPlan);
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("## Decision");
        report.AppendLine();
        report.AppendLine("- The experiment mutates only the disposable Testcontainers database.");
        report.AppendLine("- The measured result supports the production migration; it does not replace migration or post-migration validation.");
        return report;
    }

    private static void AppendMeasurement(
        StringBuilder report,
        string phase,
        ApiMeasurement measurement)
    {
        report.AppendLine(
            $"| {phase} | {measurement.Requested} | {measurement.Failed} | {Format(measurement.P50)} | {Format(measurement.P95)} | {Format(measurement.P99)} | {Format(measurement.Average)} |");
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
            $"task-pagination-candidate-{DateTime.UtcNow:yyyyMMdd-HHmmssZ}.md");
        await File.WriteAllTextAsync(path, report.ToString());
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
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

    private sealed record RequestMeasurement(bool Success, double LatencyMs);

    private sealed record ApiMeasurement(
        int Requested,
        int Failed,
        double? P50,
        double? P95,
        double? P99,
        double? Average);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class V6CandidateExperimentFactAttribute : FactAttribute
{
    public V6CandidateExperimentFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("V6_RUN_QUERY_PLAN_CANDIDATE"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Set V6_RUN_QUERY_PLAN_CANDIDATE=true to run the opt-in query-plan candidate experiment.";
        }
    }
}
