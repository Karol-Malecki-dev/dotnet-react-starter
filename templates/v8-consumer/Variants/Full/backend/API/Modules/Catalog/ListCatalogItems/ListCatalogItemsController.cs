using Application.Modules.Catalog.ListCatalogItems;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace API.Modules.Catalog.ListCatalogItems;

/// <summary>
/// Exposes the reference catalog through the canonical sender boundary.
/// </summary>
[ApiController]
[Route("api/catalog/items")]
public sealed class ListCatalogItemsController : ControllerBase
{
    private readonly ISender _sender;

    public ListCatalogItemsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns the reference catalog items.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItem>>> Get(
        CancellationToken cancellationToken)
        => Ok(await _sender.Send(new ListCatalogItemsQuery(), cancellationToken));
}
