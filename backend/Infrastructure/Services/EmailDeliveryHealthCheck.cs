using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace Infrastructure.Services;

public sealed class EmailDeliveryHealthCheck : IHealthCheck
{
    private readonly EmailDeliveryHealthState _healthState;
    private readonly EmailDeliverySettings _settings;

    public EmailDeliveryHealthCheck(
        EmailDeliveryHealthState healthState,
        IOptions<EmailDeliverySettings> settings)
    {
        _healthState = healthState;
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Email delivery is disabled."));
        }

        return Task.FromResult(_healthState.HasFailure
            ? HealthCheckResult.Unhealthy("The application recorded a failed email delivery attempt.")
            : HealthCheckResult.Healthy("No failed email delivery attempt has been recorded."));
    }
}
