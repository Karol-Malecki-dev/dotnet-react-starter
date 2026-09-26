using Application.Modules.StarterHealth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Modules.StarterHealth;

/// <summary>
/// Exposes the starter health proof through the canonical sender boundary.
/// </summary>
[ApiController]
[Route("api/starter-health")]
public sealed class StarterHealthController : ControllerBase
{
    private readonly ISender _sender;

    public StarterHealthController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns the result produced by the registered MediatR handler.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<StarterHealthResult>> Get(CancellationToken cancellationToken)
        => Ok(await _sender.Send(new GetStarterHealthQuery(), cancellationToken));
}
