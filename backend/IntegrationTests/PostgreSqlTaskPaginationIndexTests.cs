using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlTaskPaginationIndexTests
{
    private const string IndexName = "IX_ProjectTasks_ProjectId_CreatedAt";

    private readonly PostgreSqlWebApplicationFactory _factory;

    public PostgreSqlTaskPaginationIndexTests(PostgreSqlWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostgreSql_task_pagination_index_is_applied_with_descending_created_at()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.OpenConnectionAsync();

        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                $"""
                SELECT indexdef
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename = 'ProjectTasks'
                  AND indexname = '{IndexName}';
                """;

            var indexDefinition = await command.ExecuteScalarAsync();

            Assert.NotNull(indexDefinition);
            Assert.Contains(
                "\"ProjectId\", \"CreatedAt\" DESC",
                indexDefinition.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
