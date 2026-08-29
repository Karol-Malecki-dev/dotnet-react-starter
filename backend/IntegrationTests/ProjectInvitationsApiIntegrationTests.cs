using Application.DTOs.Auth;
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

public sealed class ProjectInvitationsApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public ProjectInvitationsApiIntegrationTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Owner_can_create_and_recipient_can_accept_a_project_invitation()
    {
        var ownerId = await SeedUserAsync("invite.owner@example.com", "password123", "Invite Owner");
        var recipientId = await SeedUserAsync("invite.recipient@example.com", "password123", "Invite Recipient");
        await SeedUserAsync("invite.outsider@example.com", "password123", "Invite Outsider");
        var projectId = await SeedProjectAsync(ownerId, "Invitation project");

        var ownerTokens = await LoginAsync("invite.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/invitations", new
        {
            Email = "invite.recipient@example.com",
            Role = ProjectMemberRole.Viewer
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreatedProjectInvitationResponse>>();
        Assert.NotNull(created?.Data);
        Assert.NotEmpty(created.Data.Token);
        Assert.Equal(ProjectInvitationStatus.Pending, created.Data.Invitation.Status);

        var ownerListResponse = await _client.GetAsync($"/api/projects/{projectId}/invitations");
        ownerListResponse.EnsureSuccessStatusCode();
        var ownerInvitations = await ownerListResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectInvitationResponse>>>();
        Assert.NotNull(ownerInvitations?.Data);
        Assert.Single(ownerInvitations.Data);

        var outsiderTokens = await LoginAsync("invite.outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        var outsiderAcceptResponse = await _client.PostAsJsonAsync("/api/project-invitations/accept", new { Token = created.Data.Token });
        Assert.Equal(HttpStatusCode.NotFound, outsiderAcceptResponse.StatusCode);

        var recipientTokens = await LoginAsync("invite.recipient@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recipientTokens.AccessToken);
        var mineResponse = await _client.GetAsync("/api/project-invitations/mine");
        mineResponse.EnsureSuccessStatusCode();
        var myInvitations = await mineResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectInvitationResponse>>>();
        Assert.NotNull(myInvitations?.Data);
        Assert.Single(myInvitations.Data);
        Assert.Equal(recipientId, myInvitations.Data[0].InvitedUserId);

        var acceptResponse = await _client.PostAsJsonAsync("/api/project-invitations/accept", new { Token = created.Data.Token });
        acceptResponse.EnsureSuccessStatusCode();
        var accepted = await acceptResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectInvitationResponse>>();
        Assert.Equal(ProjectInvitationStatus.Accepted, accepted?.Data?.Status);

        var membersResponse = await _client.GetAsync($"/api/projects/{projectId}/members");
        membersResponse.EnsureSuccessStatusCode();
        var members = await membersResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectMemberResponse>>>();
        Assert.Contains(members?.Data ?? [], member => member.UserId == recipientId && member.Role == ProjectMemberRole.Viewer);
    }

    private async Task<AuthTokenResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokenResponse>>();
        Assert.NotNull(result?.Data);
        return result.Data;
    }

    private async Task<Guid> SeedUserAsync(string email, string password, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = EmailAddress.Create(email),
            DisplayName = displayName,
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
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
}
