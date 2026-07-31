using Application.DTOs.Auth;
using API.Contracts.Projects;
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

public class ProjectTasksApiIntegrationTests
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProjectTasksApiIntegrationTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetTasks_Returns_unauthorized_when_token_is_missing()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_update_status_and_delete_project_task()
    {
        var ownerId = await SeedUserAsync("task.owner@example.com", "password123", "Task Owner");
        var projectId = await SeedProjectAsync(ownerId, "Task project");
        var tokens = await LoginAsync("task.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            Title = "Prepare release notes",
            Description = "Document the first release",
            Priority = ProjectTaskPriority.High,
            DueDate = DateTime.UtcNow.AddDays(3)
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(projectId, created.Data.ProjectId);
        Assert.Equal(ProjectTaskStatus.Todo, created.Data.Status);
        Assert.Equal(ProjectTaskPriority.High, created.Data.Priority);

        var statusResponse = await _client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{created.Data.Id}/status",
            new { Status = ProjectTaskStatus.InProgress });

        statusResponse.EnsureSuccessStatusCode();
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.Equal(ProjectTaskStatus.InProgress, statusResult?.Data?.Status);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{created.Data.Id}",
            new
            {
                Title = "Prepare final release notes",
                Description = "Updated description",
                Priority = ProjectTaskPriority.Normal,
                DueDate = (DateTime?)null
            });

        updateResponse.EnsureSuccessStatusCode();
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.Equal("Prepare final release notes", updateResult?.Data?.Title);
        Assert.Equal(ProjectTaskStatus.InProgress, updateResult?.Data?.Status);

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{projectId}/tasks/{created.Data.Id}");

        deleteResponse.EnsureSuccessStatusCode();
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.True(deleteResult?.Data);
    }

    [Fact]
    public async Task User_cannot_access_tasks_in_another_users_project()
    {
        var ownerId = await SeedUserAsync("task.private-owner@example.com", "password123", "Private Task Owner");
        await SeedUserAsync("task.private-other@example.com", "password123", "Private Task Other");
        var projectId = await SeedProjectAsync(ownerId, "Private task project");

        var tokens = await LoginAsync("task.private-other@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var listResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks");
        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            Title = "Unauthorized task"
        });

        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
    }

    [Fact]
    public async Task User_cannot_create_tasks_in_an_archived_project()
    {
        var ownerId = await SeedUserAsync("task.archived-owner@example.com", "password123", "Archived Task Owner");
        var projectId = await SeedProjectAsync(ownerId, "Archived task project");
        var tokens = await LoginAsync("task.archived-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var archiveResponse = await _client.DeleteAsync($"/api/projects/{projectId}");
        archiveResponse.EnsureSuccessStatusCode();

        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            Title = "Task in archived project"
        });

        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
    }

    [Fact]
    public async Task Owner_can_see_project_members()
    {
        var ownerId = await SeedUserAsync("members.owner@example.com", "password123", "Members Owner");
        var memberId = await SeedUserAsync("members.member@example.com", "password123", "Members Member");
        var projectId = await SeedProjectAsync(ownerId, "Members project");
        await SeedProjectMemberAsync(projectId, memberId);

        var tokens = await LoginAsync("members.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.GetAsync($"/api/projects/{projectId}/members");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ProjectMemberResponse>>>();
        Assert.NotNull(result?.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, member => member.UserId == ownerId);
        Assert.Contains(result.Data, member => member.UserId == memberId);
    }

    [Fact]
    public async Task User_outside_project_cannot_see_project_members()
    {
        var ownerId = await SeedUserAsync("members.private-owner@example.com", "password123", "Private Members Owner");
        await SeedUserAsync("members.private-other@example.com", "password123", "Private Members Other");
        var projectId = await SeedProjectAsync(ownerId, "Private members project");

        var tokens = await LoginAsync("members.private-other@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.GetAsync($"/api/projects/{projectId}/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_cannot_assign_task_to_user_outside_project()
    {
        var ownerId = await SeedUserAsync("assignment.owner@example.com", "password123", "Assignment Owner");
        var outsiderId = await SeedUserAsync("assignment.outsider@example.com", "password123", "Assignment Outsider");
        var projectId = await SeedProjectAsync(ownerId, "Assignment project");
        var tokens = await LoginAsync("assignment.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            Title = "Invalid assignment",
            AssignedUserId = outsiderId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Removing_member_clears_their_task_assignments()
    {
        var ownerId = await SeedUserAsync("assignment.remove-owner@example.com", "password123", "Removal Owner");
        var memberId = await SeedUserAsync("assignment.remove-member@example.com", "password123", "Removal Member");
        var projectId = await SeedProjectAsync(ownerId, "Removal project");
        await SeedProjectMemberAsync(projectId, memberId);

        var tokens = await LoginAsync("assignment.remove-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new
        {
            Title = "Assigned task",
            AssignedUserId = memberId
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(memberId, created.Data.AssignedUserId);

        var removeResponse = await _client.DeleteAsync($"/api/projects/{projectId}/members/{memberId}");
        removeResponse.EnsureSuccessStatusCode();

        var tasksResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks");
        tasksResponse.EnsureSuccessStatusCode();
        var tasks = await tasksResponse.Content.ReadFromJsonAsync<ApiResponse<PagedProjectTaskResponse>>();
        Assert.NotNull(tasks?.Data);
        Assert.Equal(1, tasks.Data.TotalCount);
        var task = Assert.Single(tasks.Data.Items);
        Assert.Null(task.AssignedUserId);
    }

    [Fact]
    public async Task Members_can_discuss_tasks_while_viewers_and_outsiders_cannot_modify_comments()
    {
        var ownerId = await SeedUserAsync("comments.owner@example.com", "password123", "Comments Owner");
        var memberId = await SeedUserAsync("comments.member@example.com", "password123", "Comments Member");
        var viewerId = await SeedUserAsync("comments.viewer@example.com", "password123", "Comments Viewer");
        await SeedUserAsync("comments.outsider@example.com", "password123", "Comments Outsider");
        var projectId = await SeedProjectAsync(ownerId, "Comments project");
        await SeedProjectMemberAsync(projectId, memberId);
        await SeedProjectMemberAsync(projectId, viewerId, ProjectMemberRole.Viewer);

        var ownerTokens = await LoginAsync("comments.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var taskResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { Title = "Discuss scope" });
        taskResponse.EnsureSuccessStatusCode();
        var task = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(task?.Data);

        var memberTokens = await LoginAsync("comments.member@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberTokens.AccessToken);
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/comments",
            new { Content = "  I can take this part.  " });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskCommentResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(memberId, created.Data.AuthorUserId);
        Assert.Equal("I can take this part.", created.Data.Content);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var listResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/comments");
        listResponse.EnsureSuccessStatusCode();
        var comments = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectTaskCommentResponse>>>();
        Assert.NotNull(comments?.Data);
        Assert.Single(comments.Data);

        var viewerTokens = await LoginAsync("comments.viewer@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerTokens.AccessToken);
        var viewerCreateResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/comments",
            new { Content = "Viewer comment" });
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreateResponse.StatusCode);

        var outsiderTokens = await LoginAsync("comments.outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        var outsiderResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/comments");
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var ownerCommentResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/comments",
            new { Content = "Owner comment" });
        ownerCommentResponse.EnsureSuccessStatusCode();
        var ownerComment = await ownerCommentResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskCommentResponse>>();
        Assert.NotNull(ownerComment?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberTokens.AccessToken);
        var forbiddenDeleteResponse = await _client.DeleteAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/comments/{ownerComment.Data.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDeleteResponse.StatusCode);

        var ownDeleteResponse = await _client.DeleteAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/comments/{created.Data.Id}");
        ownDeleteResponse.EnsureSuccessStatusCode();
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
        dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId
        });
        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedProjectMemberAsync(Guid projectId, Guid userId, ProjectMemberRole role = ProjectMemberRole.Member)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role
        });
        await dbContext.SaveChangesAsync();
    }
}