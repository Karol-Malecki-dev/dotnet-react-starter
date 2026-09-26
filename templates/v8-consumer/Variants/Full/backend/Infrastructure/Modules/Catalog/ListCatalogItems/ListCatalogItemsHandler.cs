using Application.Modules.Catalog.ListCatalogItems;
using MediatR;

namespace Infrastructure.Modules.Catalog.ListCatalogItems;

/// <summary>
/// Handles the reference catalog query without inventing persistence rules.
/// </summary>
public sealed class ListCatalogItemsHandler
    : IRequestHandler<ListCatalogItemsQuery, IReadOnlyList<CatalogItem>>
{
    /// <inheritdoc />
    public Task<IReadOnlyList<CatalogItem>> Handle(
        ListCatalogItemsQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CatalogItem>>(
            [new CatalogItem("Reference module")]);
}
