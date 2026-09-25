using API.Middleware;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Globalization;

namespace IntegrationTests;

public sealed class ObservabilityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ObservabilityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoints_report_liveness_and_readiness_with_a_correlation_id()
    {
        const string correlationId = "integration-correlation-id";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var readyResponse = await _client.SendAsync(request);
        var liveResponse = await _client.GetAsync("/health/live");
        var workersResponse = await _client.GetAsync("/health/workers");
        var storageResponse = await _client.GetAsync("/health/storage");
        var malwareScannerResponse = await _client.GetAsync("/health/malware-scanner");
        var emailResponse = await _client.GetAsync("/health/email");

        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, storageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, malwareScannerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, emailResponse.StatusCode);
        Assert.Equal(correlationId, Assert.Single(readyResponse.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
        Assert.True(liveResponse.Headers.Contains(CorrelationIdMiddleware.HeaderName));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            dbContext.NotificationEmailOutboxMessages.AddRange(
                new NotificationEmailOutboxMessage
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now.AddSeconds(-90),
                    NextAttemptAt = now,
                    ProcessingLeaseId = Guid.NewGuid(),
                    ProcessingLeaseExpiresAt = now.AddMinutes(5)
                },
                new NotificationEmailOutboxMessage
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = now.AddMinutes(-5),
                    NextAttemptAt = now.AddMinutes(-4),
                    AttemptCount = NotificationEmailOutboxMessage.MaxAttempts,
                    DeadLetteredAt = now.AddMinutes(-2)
                });
            await dbContext.SaveChangesAsync();
        }

        var metricsResponse = await _client.GetAsync("/metrics");
        var metricsPayload = await metricsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        Assert.Contains("text/plain", metricsResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "notification_email_outbox_pending_messages 1",
            metricsPayload);
        Assert.Contains(
            "notification_email_outbox_dead_letter_messages 1",
            metricsPayload);

        var oldestPendingAgeLine = metricsPayload
            .Split(Environment.NewLine)
            .Single(line => line.StartsWith(
                "notification_email_outbox_oldest_pending_message_age_seconds ",
                StringComparison.Ordinal));
        var oldestPendingAge = double.Parse(
            oldestPendingAgeLine.Split(' ', 2)[1],
            CultureInfo.InvariantCulture);
        Assert.InRange(oldestPendingAge, 80, 180);
    }
}