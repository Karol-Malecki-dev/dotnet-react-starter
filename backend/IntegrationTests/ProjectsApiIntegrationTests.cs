using Application.DTOs.Auth;
using Application.Features.Projects;
using API.Contracts.Projects;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
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
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectResponse>>();

        Assert.NotNull(created?.Data);
        Assert.Equal(ownerId, created.Data.OwnerId);
        Assert.Equal("Website redesign", created.Data.Name);
        Assert.False(created.Data.IsArchived);

        var listResponse = await _client.GetAsync("/api/projects");
        listResponse.EnsureSuccessStatusCode();
        var projects = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectResponse>>>();

        Assert.NotNull(projects?.Data);
        var project = Assert.Single(projects.Data);
        Assert.Equal("Website redesign", project.Name);
    }

    [Fact]
    public async Task Project_update_rejects_a_stale_concurrency_stamp()
    {
        var ownerId = await SeedUserAsync("project.concurrency-owner@example.com", "password123", "Concurrency Owner");
        var projectId = await SeedProjectAsync(ownerId, "Concurrency project");
        var tokens = await LoginAsync("project.concurrency-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var projectResponse = await _client.GetAsync($"/api/projects/{projectId}");
        projectResponse.EnsureSuccessStatusCode();
        var project = await projectResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectResponse>>();
        Assert.NotNull(project?.Data);
        Assert.False(string.IsNullOrWhiteSpace(project.Data.ConcurrencyStamp));

        var firstUpdateResponse = await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            Name = "First update",
            Description = "The current version wins",
            ConcurrencyStamp = project.Data.ConcurrencyStamp
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdateResponse.StatusCode);

        var staleUpdateResponse = await _client.PutAsJsonAsync($"/api/projects/{projectId}", new
        {
            Name = "Stale update",
            Description = "This must be rejected",
            ConcurrencyStamp = project.Data.ConcurrencyStamp
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

        var currentProjectResponse = await _client.GetAsync($"/api/projects/{projectId}");
        currentProjectResponse.EnsureSuccessStatusCode();
        var currentProject = await currentProjectResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectResponse>>();
        Assert.Equal("First update", currentProject?.Data?.Name);
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
        var archivedProject = await archivedProjectDetailsResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectResponse>>();
        Assert.True(archivedProject?.Data?.IsArchived);

        var activeProjectsResponse = await _client.GetAsync("/api/projects");
        activeProjectsResponse.EnsureSuccessStatusCode();
        var activeProjects = await activeProjectsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectResponse>>>();
        Assert.Empty(activeProjects?.Data ?? []);

        var allProjectsResponse = await _client.GetAsync("/api/projects?includeArchived=true");
        allProjectsResponse.EnsureSuccessStatusCode();
        var allProjects = await allProjectsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectResponse>>>();
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

    [Fact]
    public async Task Project_activity_is_paged_and_hidden_from_users_without_project_access()
    {
        await SeedUserAsync("activity.owner@example.com", "password123", "Activity Owner");
        var memberId = await SeedUserAsync("activity.member@example.com", "password123", "Activity Member");
        await SeedUserAsync("activity.outsider@example.com", "password123", "Activity Outsider");

        var ownerTokens = await LoginAsync("activity.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new { Name = "Activity project", Description = "Timeline" });
        var createdProject = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectResponse>>();
        Assert.NotNull(createdProject?.Data);

        var addMemberResponse = await _client.PostAsJsonAsync($"/api/projects/{createdProject.Data.Id}/members", new { UserId = memberId });
        Assert.Equal(HttpStatusCode.Created, addMemberResponse.StatusCode);

        var activityResponse = await _client.GetAsync($"/api/projects/{createdProject.Data.Id}/activity?pageNumber=1&pageSize=1");
        activityResponse.EnsureSuccessStatusCode();
        var activity = await activityResponse.Content.ReadFromJsonAsync<ApiResponse<PagedProjectActivityView>>();
        Assert.NotNull(activity?.Data);
        Assert.Single(activity.Data.Items);
        Assert.Equal(2, activity.Data.TotalCount);
        Assert.Equal("member.added", activity.Data.Items[0].Type);

        var outsiderTokens = await LoginAsync("activity.outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        var forbiddenResponse = await _client.GetAsync($"/api/projects/{createdProject.Data.Id}/activity");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Project_dashboard_returns_task_metrics_and_due_date_lists()
    {
        var ownerId = await SeedUserAsync("dashboard.owner@example.com", "password123", "Dashboard Owner");
        await SeedUserAsync("dashboard.outsider@example.com", "password123", "Dashboard Outsider");
        var projectId = await SeedProjectAsync(ownerId, "Dashboard project");
        await SeedProjectTaskAsync(projectId, "Overdue task", ProjectTaskStatus.Todo, ProjectTaskPriority.High, DateTime.UtcNow.AddDays(-1));
        await SeedProjectTaskAsync(projectId, "Upcoming task", ProjectTaskStatus.InProgress, ProjectTaskPriority.Normal, DateTime.UtcNow.AddDays(3));
        await SeedProjectTaskAsync(projectId, "Completed task", ProjectTaskStatus.Done, ProjectTaskPriority.Low, DateTime.UtcNow.AddDays(-2));

        var ownerTokens = await LoginAsync("dashboard.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var dashboardResponse = await _client.GetAsync($"/api/projects/{projectId}/dashboard");
        dashboardResponse.EnsureSuccessStatusCode();
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectDashboardView>>();
        Assert.NotNull(dashboard?.Data);
        Assert.Equal(3, dashboard.Data.TotalTasks);
        Assert.Equal(1, dashboard.Data.TodoTasks);
        Assert.Equal(1, dashboard.Data.InProgressTasks);
        Assert.Equal(1, dashboard.Data.DoneTasks);
        Assert.Single(dashboard.Data.OverdueTasks);
        Assert.Single(dashboard.Data.UpcomingTasks);

        var outsiderTokens = await LoginAsync("dashboard.outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        var forbiddenResponse = await _client.GetAsync($"/api/projects/{projectId}/dashboard");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);
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
        var user = User.Create(
            EmailAddress.Create(email),
            DisplayName.Create(displayName),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true);
        user.SetPasswordHash(passwordHasher.HashPassword(user, password));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> SeedProjectAsync(Guid ownerId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = Project.Create(ownerId, name);

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedProjectTaskAsync(Guid projectId, string title, ProjectTaskStatus status, ProjectTaskPriority priority, DateTime dueDate)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = ProjectTask.Create(projectId, title, null, priority, dueDate, null, null);
        task.ChangeStatus(status);
        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync();
    }
}