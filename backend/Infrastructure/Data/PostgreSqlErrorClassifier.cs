using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Data;

internal static class PostgreSqlErrorClassifier
{
    public static bool IsUniqueConstraintViolation(
        DbUpdateException exception,
        string constraintName)
        => exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && postgresException.ConstraintName == constraintName;
}
