using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Dispatching;

/// <summary>
/// Records safe MediatR request telemetry without serializing request payloads.
/// </summary>
public sealed class MediatRTelemetryBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<MediatRTelemetryBehavior<TRequest, TResponse>> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MediatRTelemetryBehavior(
        ILogger<MediatRTelemetryBehavior<TRequest, TResponse>> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest).FullName ?? typeof(TRequest).Name;
        var correlationId = GetCorrelationId();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            _logger.LogInformation(
                "MediatR request {RequestType} completed in {DurationMilliseconds} ms with outcome {Outcome}. CorrelationId: {CorrelationId}",
                requestType,
                stopwatch.Elapsed.TotalMilliseconds,
                "completed",
                correlationId);
            return response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "MediatR request {RequestType} cancelled in {DurationMilliseconds} ms with outcome {Outcome}. CorrelationId: {CorrelationId}",
                requestType,
                stopwatch.Elapsed.TotalMilliseconds,
                "cancelled",
                correlationId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "MediatR request {RequestType} failed in {DurationMilliseconds} ms with outcome {Outcome} and exception type {ExceptionType}. CorrelationId: {CorrelationId}",
                requestType,
                stopwatch.Elapsed.TotalMilliseconds,
                "failed",
                exception.GetType().FullName,
                correlationId);
            throw;
        }
    }

    private string? GetCorrelationId()
        => _httpContextAccessor.HttpContext?.TraceIdentifier
            ?? Activity.Current?.TraceId.ToString();
}
