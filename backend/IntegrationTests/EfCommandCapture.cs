using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IntegrationTests;

public sealed record EfCommandObservation(
    string Kind,
    double DurationMs,
    string CommandText);

public sealed record EfRequestCapture(
    IReadOnlyList<EfCommandObservation> Commands)
{
    public int CommandCount => Commands.Count;

    public double TotalDurationMs =>
        Commands.Sum(command => command.DurationMs);
}

public sealed class EfCommandCapture : DbCommandInterceptor
{
    private readonly object _gate = new();
    private List<EfCommandObservation> _commands = [];
    private bool _isCapturing;

    public void Start()
    {
        lock (_gate)
        {
            if (_isCapturing)
            {
                throw new InvalidOperationException("EF command capture is already active.");
            }

            _commands = [];
            _isCapturing = true;
        }
    }

    public EfRequestCapture Stop()
    {
        lock (_gate)
        {
            if (!_isCapturing)
            {
                throw new InvalidOperationException("EF command capture is not active.");
            }

            _isCapturing = false;
            return new EfRequestCapture(_commands.ToArray());
        }
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record("Reader", command, eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record("Reader", command, eventData.Duration);
        return new ValueTask<DbDataReader>(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record("Scalar", command, eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record("Scalar", command, eventData.Duration);
        return new ValueTask<object?>(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record("NonQuery", command, eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record("NonQuery", command, eventData.Duration);
        return new ValueTask<int>(result);
    }

    private void Record(
        string kind,
        DbCommand command,
        TimeSpan duration)
    {
        lock (_gate)
        {
            if (!_isCapturing)
            {
                return;
            }

            _commands.Add(
                new EfCommandObservation(
                    kind,
                    duration.TotalMilliseconds,
                    command.CommandText.Trim()));
        }
    }
}
