using Infrastructure.Dispatching;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UnitTests.Architecture;

public sealed class MediatRTelemetryBehaviorTests
{
    [Fact]
    public async Task Handle_logs_completion_without_serializing_request_payload()
    {
        var logger = new RecordingLogger<MediatRTelemetryBehavior<TelemetryRequest, string>>();
        var behavior = CreateBehavior(logger, "correlation-123");

        var response = await behavior.Handle(
            new TelemetryRequest("do-not-log-this"),
            _ => Task.FromResult("completed"),
            CancellationToken.None);

        Assert.Equal("completed", response);
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("TelemetryRequest", entry);
        Assert.Contains("completed", entry);
        Assert.Contains("correlation-123", entry);
        Assert.DoesNotContain("do-not-log-this", entry);
    }

    [Fact]
    public async Task Handle_logs_cancellation_and_rethrows_without_serializing_request_payload()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var logger = new RecordingLogger<MediatRTelemetryBehavior<TelemetryRequest, string>>();
        var behavior = CreateBehavior(logger, "correlation-456");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new TelemetryRequest("cancelled-payload"),
            _ => Task.FromCanceled<string>(cancellationTokenSource.Token),
            cancellationTokenSource.Token));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("cancelled", entry);
        Assert.Contains("correlation-456", entry);
        Assert.DoesNotContain("cancelled-payload", entry);
    }

    [Fact]
    public async Task Handle_logs_failure_type_and_rethrows_without_serializing_exception_details()
    {
        var logger = new RecordingLogger<MediatRTelemetryBehavior<TelemetryRequest, string>>();
        var behavior = CreateBehavior(logger, "correlation-789");
        var exception = new InvalidOperationException("secret failure details");

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TelemetryRequest("failed-payload"),
            _ => Task.FromException<string>(exception),
            CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("failed", entry);
        Assert.Contains(nameof(InvalidOperationException), entry);
        Assert.Contains("correlation-789", entry);
        Assert.DoesNotContain("failed-payload", entry);
        Assert.DoesNotContain("secret failure details", entry);
    }

    private static MediatRTelemetryBehavior<TelemetryRequest, string> CreateBehavior(
        RecordingLogger<MediatRTelemetryBehavior<TelemetryRequest, string>> logger,
        string correlationId)
        => new(
            logger,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = correlationId
                }
            });

    private sealed record TelemetryRequest(string Title) : IRequest<string>;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
