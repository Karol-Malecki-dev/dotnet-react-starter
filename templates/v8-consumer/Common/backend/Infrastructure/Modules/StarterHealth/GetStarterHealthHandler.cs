using Application.Modules.StarterHealth;
using MediatR;

namespace Infrastructure.Modules.StarterHealth;

/// <summary>
/// Handles the starter health query without introducing persistence or domain state.
/// </summary>
public sealed class GetStarterHealthHandler
    : IRequestHandler<GetStarterHealthQuery, StarterHealthResult>
{
    /// <inheritdoc />
    public Task<StarterHealthResult> Handle(
        GetStarterHealthQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(new StarterHealthResult("ok"));
}
