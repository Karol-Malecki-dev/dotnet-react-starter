using Application.Modules.StarterHealth;
using Infrastructure.Modules.StarterHealth;

namespace UnitTests;

public sealed class StarterHealthHandlerTests
{
    [Fact]
    public async Task Handler_returns_healthy_result()
    {
        var result = await new GetStarterHealthHandler()
            .Handle(new GetStarterHealthQuery(), CancellationToken.None);

        Assert.Equal("ok", result.Status);
    }
}
