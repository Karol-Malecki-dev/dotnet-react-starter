using Application.Modules.Catalog.ListCatalogItems;
using Infrastructure.Modules.Catalog.ListCatalogItems;

namespace UnitTests.Modules.Catalog.ListCatalogItems;

public sealed class ListCatalogItemsHandlerTests
{
    [Fact]
    public async Task Handler_returns_reference_module_item()
    {
        var result = await new ListCatalogItemsHandler()
            .Handle(new ListCatalogItemsQuery(), CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("Reference module", item.Name);
    }
}
