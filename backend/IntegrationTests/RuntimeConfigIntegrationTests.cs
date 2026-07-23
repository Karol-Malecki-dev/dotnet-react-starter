using System.Net.Http.Json;
using Shared.Dtos;
using Shared.Responses;

namespace IntegrationTests;

public class RuntimeConfigIntegrationTests
{
    private readonly HttpClient _client;

    public RuntimeConfigIntegrationTests()
    {
        var factory = new CustomWebApplicationFactory();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetRuntimeConfiguration_Returns_public_feature_flags_for_frontend_bootstrap()
    {
        var response = await _client.GetAsync("/api/runtime-config");

        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<AppRuntimeConfigurationDto>>();

        Assert.NotNull(apiResponse);
        Assert.NotNull(apiResponse!.Data);
        Assert.False(apiResponse.Data.Features.EmailDeliveryEnabled);
        Assert.True(apiResponse.Data.Features.GlobalSearchEnabled);
        Assert.True(apiResponse.Data.Features.DashboardOverviewEnabled);
        Assert.True(apiResponse.Data.Features.AdminNavigationEnabled);
        Assert.True(apiResponse.Data.Features.UserManagementNavigationEnabled);
        Assert.True(apiResponse.Data.Features.EmailFeatureSectionsEnabled);
        Assert.True(apiResponse.Data.Features.EmailTwoFactorEnabled);
        Assert.True(apiResponse.Data.Features.EmailTwoFactorEnabledForNewUsers);
        Assert.Equal("Runtime configuration loaded", apiResponse.Message);
    }
}