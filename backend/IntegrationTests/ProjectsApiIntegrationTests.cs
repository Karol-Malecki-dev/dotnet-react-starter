using Application.DTOs.Auth;
using Application.DTOs.Project;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IntegrationTests;

public class ProjectsApiIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProjectsApiIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetProjects_Returns_unauthorized_when_token_is_missing()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_can_create_and_list_only_their_own_projects()
    {
        var ownerId = await SeedUserAsync("project.owner@example.com", "password123", "Project Owner");
        await SeedUserAsync("project.other@example.com", "password123", "Project Other");

        var tokens = await LoginAsync("project.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = "Website redesign",
            Description = "Plan the next release"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectDto>>();

        Assert.NotNull(created?.Data);
        Assert.Equal(ownerId, created.Data.OwnerId);
        Assert.Equal("Website redesign", created.Data.Name);
        Assert.False(created.Data.IsArchived);

        var listResponse = await _client.GetAsync("/api/projects");
        listResponse.EnsureSuccessStatusCode();
        var projects = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectDto>>>();

        Assert.NotNull(projects?.Data);
        var project = Assert.Single(projects.Data);
        Assert.Equal("Website redesign", project.Name);
    }

    [Fact]
    public async Task User_cannot_access_another_users_project_and_can_archive_their_own_project()
    {
        var ownerId = await SeedUserAsync("project.archive-owner@example.com", "password123", "Archive Owner");
        var otherUserId = await SeedUserAsync("project.archive-other@example.com", "password123", "Archive Other");
        var projectId = await SeedProjectAsync(ownerId, "Private project");

        var otherUserTokens = await LoginAsync("project.archive-other@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherUserTokens.AccessToken);

        var forbiddenProjectResponse = await _client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenProjectResponse.StatusCode);

        var ownerTokens = await LoginAsync("project.archive-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);

        var archiveResponse = await _client.DeleteAsync($"/api/projects/{projectId}");

        archiveResponse.EnsureSuccessStatusCode();
        var archiveResult = await archiveResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.True(archiveResult?.Data);

        var archivedProjectResponse = await _client.GetAsync($"/api/projects/{projectId}");
        Assert.Equal(HttpStatusCode.NotFound, archivedProjectResponse.StatusCode);

        var archivedProjectDetailsResponse = await _client.GetAsync($"/api/projects/{projectId}?includeArchived=true");
        archivedProjectDetailsResponse.EnsureSuccessStatusCode();
        var archivedProject = await archivedProjectDetailsResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectDto>>();
        Assert.True(archivedProject?.Data?.IsArchived);

        var activeProjectsResponse = await _client.GetAsync("/api/projects");
        activeProjectsResponse.EnsureSuccessStatusCode();
        var activeProjects = await activeProjectsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectDto>>>();
        Assert.Empty(activeProjects?.Data ?? []);

        var allProjectsResponse = await _client.GetAsync("/api/projects?includeArchived=true");
        allProjectsResponse.EnsureSuccessStatusCode();
        var allProjects = await allProjectsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectDto>>>();
        Assert.Single(allProjects?.Data ?? []);
        Assert.True(allProjects!.Data![0].IsArchived);
        Assert.NotEqual(otherUserId, allProjects.Data[0].OwnerId);
    }

    [Fact]
    public async Task User_cannot_update_or_archive_another_users_project()
    {
        var ownerId = await SeedUserAsync("project.owner-security@example.com", "password123", "Project Owner Security");
        await SeedUserAsync("project.other-security@example.com", "password123", "Project Other Security");
        var projectId = await SeedProjectAsync(ownerId, "Protected project");

        var tokens = await LoginAsync("project.other-security@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var updateResponse = await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            Name = "Changed by another user",
            Description = "This must be rejected"
        });

        var archiveResponse = await _client.DeleteAsync($"/api/projects/{projectId}");

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archiveResponse.StatusCode);
    }

    private async Task<AuthTokenResponse> LoginAsync(string email, string password)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();

        var apiResponse = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(apiResponse?.Data);
        return apiResponse.Data;
    }

    private async Task<Guid> SeedUserAsync(string email, string password, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = new PasswordHasher<User>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedProjectAsync(Guid ownerId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = new Project
        {
            OwnerId = ownerId,
            Name = name
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project.Id;
    }
}