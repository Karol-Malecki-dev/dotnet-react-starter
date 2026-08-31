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
using System.Security.Cryptography;
using System.Text;

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

        var duplicateCreateResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new
            {
                Email = "invite.recipient@example.com",
                Role = ProjectMemberRole.Member
            });
        Assert.Equal(HttpStatusCode.Conflict, duplicateCreateResponse.StatusCode);

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

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedInvitation = Assert.Single(verificationContext.ProjectInvitations.Where(
            invitation => invitation.ProjectId == projectId));
        Assert.Equal(HashToken(created.Data.Token), storedInvitation.TokenHash);
        Assert.NotEqual(created.Data.Token, storedInvitation.TokenHash);
        Assert.Equal(2, verificationContext.Notifications.Count(
            notification => notification.ResourceType == "ProjectInvitation"
                && notification.ResourceId == storedInvitation.Id));
        Assert.Equal(2, verificationContext.NotificationEmailOutboxMessages.Count(
            message => verificationContext.Notifications
                .Where(notification => notification.ResourceId == storedInvitation.Id)
                .Select(notification => notification.Id)
                .Contains(message.NotificationId)));
        Assert.Contains(verificationContext.ProjectActivities, activity =>
            activity.ProjectId == projectId && activity.Type == "invitation.created");
        Assert.Contains(verificationContext.ProjectActivities, activity =>
            activity.ProjectId == projectId && activity.Type == "invitation.accepted");
    }

    [Fact]
    public async Task Non_owner_cannot_list_or_create_project_invitations()
    {
        var ownerId = await SeedUserAsync("invite.access.owner@example.com", "password123", "Access Owner");
        await SeedUserAsync("invite.access.outsider@example.com", "password123", "Access Outsider");
        await SeedUserAsync("invite.access.recipient@example.com", "password123", "Access Recipient");
        var projectId = await SeedProjectAsync(ownerId, "Invitation access project");
        var outsiderTokens = await LoginAsync("invite.access.outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);

        var listResponse = await _client.GetAsync($"/api/projects/{projectId}/invitations");
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new
            {
                Email = "invite.access.recipient@example.com",
                Role = ProjectMemberRole.Member
            });

        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_invitation_requests_return_bad_request_without_persistence()
    {
        var ownerId = await SeedUserAsync("invite.validation.owner@example.com", "password123", "Validation Owner");
        var projectId = await SeedProjectAsync(ownerId, "Invitation validation project");
        var ownerTokens = await LoginAsync("invite.validation.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);

        var invalidEmailResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new
            {
                Email = "not-an-email",
                Role = ProjectMemberRole.Member
            });
        var invalidRoleResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new
            {
                Email = "recipient@example.com",
                Role = ProjectMemberRole.Owner
            });
        var emptyTokenResponse = await _client.PostAsJsonAsync(
            "/api/project-invitations/accept",
            new { Token = string.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, invalidEmailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyTokenResponse.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(
            verificationContext.ProjectInvitations,
            invitation => invitation.ProjectId == projectId);
    }

    [Fact]
    public async Task Recipient_can_decline_without_becoming_a_project_member()
    {
        var ownerId = await SeedUserAsync("invite.decline.owner@example.com", "password123", "Decline Owner");
        var recipientId = await SeedUserAsync("invite.decline.recipient@example.com", "password123", "Decline Recipient");
        var projectId = await SeedProjectAsync(ownerId, "Invitation decline project");

        var ownerTokens = await LoginAsync("invite.decline.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/invitations",
            new
            {
                Email = "invite.decline.recipient@example.com",
                Role = ProjectMemberRole.Member
            });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreatedProjectInvitationResponse>>();
        Assert.NotNull(created?.Data);

        var recipientTokens = await LoginAsync("invite.decline.recipient@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", recipientTokens.AccessToken);
        var declineResponse = await _client.PostAsJsonAsync(
            "/api/project-invitations/decline",
            new { created.Data.Token });

        declineResponse.EnsureSuccessStatusCode();
        var declined = await declineResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectInvitationResponse>>();
        Assert.Equal(ProjectInvitationStatus.Declined, declined?.Data?.Status);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(
            verificationContext.ProjectMembers,
            member => member.ProjectId == projectId && member.UserId == recipientId);
        Assert.Contains(verificationContext.ProjectActivities, activity =>
            activity.ProjectId == projectId && activity.Type == "invitation.declined");
    }

    [Fact]
    public async Task Invitation_endpoints_require_authentication()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/project-invitations/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        var user = User.Create(
            EmailAddress.Create(email),
            DisplayName.Create(displayName),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true);
        user.SetPasswordHash(new PasswordHasher<User>().HashPassword(user, password));
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

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
