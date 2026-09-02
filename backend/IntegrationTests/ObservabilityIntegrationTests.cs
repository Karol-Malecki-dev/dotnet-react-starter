using API.Middleware;
using System.Net;

namespace IntegrationTests;

public sealed class ObservabilityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ObservabilityIntegrationTests(CustomWebApplicationFactory factory)
    {
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
    }
}