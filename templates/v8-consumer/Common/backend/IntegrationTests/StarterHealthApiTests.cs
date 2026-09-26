using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IntegrationTests;

public sealed class StarterHealthApiTests
{
    [Fact]
    public async Task Starter_health_endpoint_uses_the_registered_mediatr_handler()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/starter-health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", await response.Content.ReadAsStringAsync());
    }
}
