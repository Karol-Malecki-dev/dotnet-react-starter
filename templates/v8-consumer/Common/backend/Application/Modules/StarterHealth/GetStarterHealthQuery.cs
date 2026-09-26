using MediatR;

namespace Application.Modules.StarterHealth;

/// <summary>
/// Requests the bounded starter health result used by the consumer proof.
/// </summary>
public sealed record GetStarterHealthQuery : IRequest<StarterHealthResult>;

/// <summary>
/// Represents the application result returned by the starter health query.
/// </summary>
public sealed record StarterHealthResult(string Status);
