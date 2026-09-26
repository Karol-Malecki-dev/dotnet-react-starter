using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests.Modules.Catalog.ListCatalogItems;

public sealed class ListCatalogItemsApiTests
{
    [Fact]
    public async Task Catalog_endpoint_returns_the_reference_module_item()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/catalog/items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"Reference module\"", await response.Content.ReadAsStringAsync());
    }
}
