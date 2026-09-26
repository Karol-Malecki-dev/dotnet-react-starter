using MediatR;

namespace Application.Modules.Catalog.ListCatalogItems;

/// <summary>
/// Requests the reference catalog items exposed by the full consumer variant.
/// </summary>
public sealed record ListCatalogItemsQuery : IRequest<IReadOnlyList<CatalogItem>>;

/// <summary>
/// Represents one item in the reference catalog.
/// </summary>
public sealed record CatalogItem(string Name);
