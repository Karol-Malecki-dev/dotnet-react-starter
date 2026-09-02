using Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace UnitTests.Infrastructure;

public sealed class EmailDeliveryHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_is_healthy_when_email_delivery_is_disabled()
    {
        var healthCheck = new EmailDeliveryHealthCheck(
            new EmailDeliveryHealthState(),
            Options.Create(new EmailDeliverySettings { Enabled = false }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealth_reports_a_recorded_delivery_failure_until_success()
    {
        var healthState = new EmailDeliveryHealthState();
        var healthCheck = new EmailDeliveryHealthCheck(
            healthState,
            Options.Create(new EmailDeliverySettings { Enabled = true }));

        var initialResult = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        healthState.ReportFailure();
        var failureResult = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        healthState.ReportSuccess();
        var recoveredResult = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, initialResult.Status);
        Assert.Equal(HealthStatus.Unhealthy, failureResult.Status);
        Assert.Equal(HealthStatus.Healthy, recoveredResult.Status);
    }
}
