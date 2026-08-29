using Application.DTOs.Auth;
using Application.Interfaces;
using API.Contracts.Projects;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shared.Responses;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

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
            DueDate = DateTime.UtcNow.AddDays(3),
            Labels = new[] { " Release ", "documentation", "release" }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(created?.Data);
        Assert.Equal(projectId, created.Data.ProjectId);
        Assert.Equal(ProjectTaskStatus.Todo, created.Data.Status);
        Assert.Equal(ProjectTaskPriority.High, created.Data.Priority);
        Assert.False(string.IsNullOrWhiteSpace(created.Data.ConcurrencyStamp));
        Assert.Equal(new[] { "documentation", "release" }, created.Data.Labels);

        var statusResponse = await _client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{created.Data.Id}/status",
            new { Status = ProjectTaskStatus.InProgress, ConcurrencyStamp = created.Data.ConcurrencyStamp });

        statusResponse.EnsureSuccessStatusCode();
        var statusResult = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.Equal(ProjectTaskStatus.InProgress, statusResult?.Data?.Status);
        Assert.NotNull(statusResult?.Data);
        Assert.NotEqual(created.Data.ConcurrencyStamp, statusResult.Data.ConcurrencyStamp);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/tasks/{created.Data.Id}",
            new
            {
                Title = "Prepare final release notes",
                Description = "Updated description",
                Priority = ProjectTaskPriority.Normal,
                DueDate = (DateTime?)null,
                Labels = new[] { "final" },
                ConcurrencyStamp = statusResult.Data.ConcurrencyStamp
            });

        updateResponse.EnsureSuccessStatusCode();
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.Equal("Prepare final release notes", updateResult?.Data?.Title);
        Assert.Equal(ProjectTaskStatus.InProgress, updateResult?.Data?.Status);
        Assert.Equal(new[] { "final" }, updateResult?.Data?.Labels);
        Assert.NotNull(updateResult?.Data);
        Assert.NotEqual(statusResult.Data.ConcurrencyStamp, updateResult.Data.ConcurrencyStamp);

        var searchResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks?search=final");
        searchResponse.EnsureSuccessStatusCode();
        var searchResult = await searchResponse.Content.ReadFromJsonAsync<ApiResponse<PagedProjectTaskResponse>>();
        Assert.Equal(created.Data.Id, Assert.Single(searchResult?.Data?.Items ?? []).Id);

        var deleteResponse = await _client.DeleteAsync(
            $"/api/projects/{projectId}/tasks/{created.Data.Id}?concurrencyStamp={Uri.EscapeDataString(updateResult.Data.ConcurrencyStamp)}");

        deleteResponse.EnsureSuccessStatusCode();
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        Assert.True(deleteResult?.Data);
    }

    [Fact]
    public async Task Task_update_rejects_a_stale_concurrency_stamp()
    {
        var ownerId = await SeedUserAsync("task.concurrency-owner@example.com", "password123", "Task Concurrency Owner");
        var projectId = await SeedProjectAsync(ownerId, "Task concurrency project");
        var tokens = await LoginAsync("task.concurrency-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { Title = "Concurrency task" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(created?.Data);
        var staleStamp = created.Data.ConcurrencyStamp;

        var firstUpdateResponse = await _client.PutAsJsonAsync($"/api/projects/{projectId}/tasks/{created.Data.Id}", new
        {
            Title = "First task update",
            ConcurrencyStamp = staleStamp
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdateResponse.StatusCode);

        var staleUpdateResponse = await _client.PutAsJsonAsync($"/api/projects/{projectId}/tasks/{created.Data.Id}", new
        {
            Title = "Stale task update",
            ConcurrencyStamp = staleStamp
        });
        Assert.Equal(HttpStatusCode.Conflict, staleUpdateResponse.StatusCode);

        var currentResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks/{created.Data.Id}");
        currentResponse.EnsureSuccessStatusCode();
        var current = await currentResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.Equal("First task update", current?.Data?.Title);
    }

    [Fact]
    public async Task Project_task_changes_do_not_change_project_concurrency_stamp()
    {
        var ownerId = await SeedUserAsync("task.aggregate-owner@example.com", "password123", "Aggregate Owner");
        var projectId = await SeedProjectAsync(ownerId, "Aggregate boundary project");

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = await dbContext.Projects.SingleAsync(candidate => candidate.Id == projectId);
        var originalConcurrencyStamp = project.ConcurrencyStamp;
        var task = ProjectTask.Create(projectId, "Boundary task", null, ProjectTaskPriority.Normal, null, null, ownerId);

        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync();

        task.Rename("Updated boundary task");
        await dbContext.SaveChangesAsync();

        var persistedProject = await dbContext.Projects
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == projectId);

        Assert.Equal(originalConcurrencyStamp, persistedProject.ConcurrencyStamp);
    }

    [Fact]
    public async Task Owner_can_filter_and_sort_project_tasks()
    {
        var ownerId = await SeedUserAsync("task.filter-owner@example.com", "password123", "Filter Owner");
        var assigneeId = await SeedUserAsync("task.filter-assignee@example.com", "password123", "Filter Assignee");
        var projectId = await SeedProjectAsync(ownerId, "Filter task project");
        await SeedProjectMemberAsync(projectId, assigneeId);
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.ProjectTasks.AddRange(
                ProjectTask.Create(projectId, "Normal release task", null, ProjectTaskPriority.Normal, now.AddDays(2), assigneeId, ownerId, ["release"]),
                ProjectTask.Create(projectId, "High release task", null, ProjectTaskPriority.High, now.AddDays(1), assigneeId, ownerId, ["release"]),
                ProjectTask.Create(projectId, "Unrelated task", null, ProjectTaskPriority.Low, now.AddDays(5), ownerId, ownerId, ["design"]));
            await dbContext.SaveChangesAsync();
        }

        var tokens = await LoginAsync("task.filter-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/tasks?status={ProjectTaskStatus.Todo}&assignedUserId={assigneeId}&label=release&dueBefore={now.AddDays(3):O}&sortBy=Priority&sortDirection=Descending");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedProjectTaskResponse>>();
        Assert.NotNull(result?.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(["High release task", "Normal release task"], result.Data.Items.Select(task => task.Title));
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

    [Fact]
    public async Task Owner_can_upload_list_download_and_delete_task_attachment()
    {
        var ownerId = await SeedUserAsync("attachments.owner@example.com", "password123", "Attachments Owner");
        var projectId = await SeedProjectAsync(ownerId, "Attachments project");
        var tokens = await LoginAsync("attachments.owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var taskResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { Title = "Attach release notes" });
        taskResponse.EnsureSuccessStatusCode();
        var task = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(task?.Data);

        var bytes = Encoding.UTF8.GetBytes("release notes");
        var uploadResponse = await UploadAttachmentAsync(projectId, task.Data.Id, bytes, "release-notes.txt", "text/plain");

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskAttachmentResponse>>();
        Assert.NotNull(uploaded?.Data);
        Assert.Equal("release-notes.txt", uploaded.Data.OriginalFileName);
        Assert.Equal(ownerId, uploaded.Data.UploadedByUserId);
        Assert.Equal(bytes.Length, uploaded.Data.SizeBytes);

        var listResponse = await _client.GetAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments");
        listResponse.EnsureSuccessStatusCode();
        var listed = await listResponse.Content.ReadFromJsonAsync<ApiResponse<List<ProjectTaskAttachmentResponse>>>();
        Assert.NotNull(listed?.Data);
        Assert.Single(listed.Data);
        Assert.Equal(uploaded.Data.Id, listed.Data[0].Id);

        var downloadResponse = await _client.GetAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{uploaded.Data.Id}/download");
        downloadResponse.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", downloadResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await downloadResponse.Content.ReadAsByteArrayAsync());

        var deleteResponse = await _client.DeleteAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{uploaded.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.DoesNotContain(dbContext.ProjectTaskAttachments, attachment => attachment.Id == uploaded.Data.Id);
        Assert.Contains(dbContext.ProjectActivities, activity => activity.Type == "task.attachment-added");
        Assert.Contains(dbContext.ProjectActivities, activity => activity.Type == "task.attachment-removed");
    }

    [Fact]
    public async Task Attachment_upload_rejects_invalid_extension_content_type_and_size()
    {
        var ownerId = await SeedUserAsync("attachments.validation@example.com", "password123", "Attachments Validation");
        var projectId = await SeedProjectAsync(ownerId, "Attachment validation project");
        var tokens = await LoginAsync("attachments.validation@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var taskResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { Title = "Validate attachments" });
        taskResponse.EnsureSuccessStatusCode();
        var task = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(task?.Data);

        var invalidExtension = await UploadAttachmentAsync(
            projectId, task.Data.Id, Encoding.UTF8.GetBytes("not an image"), "payload.exe", "application/octet-stream");
        Assert.Equal(HttpStatusCode.BadRequest, invalidExtension.StatusCode);

        var invalidContentType = await UploadAttachmentAsync(
            projectId, task.Data.Id, Encoding.UTF8.GetBytes("plain text"), "payload.txt", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, invalidContentType.StatusCode);

        var tooLarge = await UploadAttachmentAsync(
            projectId, task.Data.Id, new byte[10 * 1024 * 1024 + 1], "large.txt", "text/plain");
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }

    [Fact]
    public async Task Members_can_upload_and_delete_own_attachment_but_viewers_and_outsiders_are_restricted()
    {
        var ownerId = await SeedUserAsync("attachments.roles-owner@example.com", "password123", "Attachments Roles Owner");
        var memberId = await SeedUserAsync("attachments.roles-member@example.com", "password123", "Attachments Roles Member");
        var viewerId = await SeedUserAsync("attachments.roles-viewer@example.com", "password123", "Attachments Roles Viewer");
        await SeedUserAsync("attachments.roles-outsider@example.com", "password123", "Attachments Roles Outsider");
        var projectId = await SeedProjectAsync(ownerId, "Attachment roles project");
        await SeedProjectMemberAsync(projectId, memberId);
        await SeedProjectMemberAsync(projectId, viewerId, ProjectMemberRole.Viewer);

        var ownerTokens = await LoginAsync("attachments.roles-owner@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var taskResponse = await _client.PostAsJsonAsync($"/api/projects/{projectId}/tasks", new { Title = "Check attachment roles" });
        taskResponse.EnsureSuccessStatusCode();
        var task = await taskResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskResponse>>();
        Assert.NotNull(task?.Data);

        var memberTokens = await LoginAsync("attachments.roles-member@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberTokens.AccessToken);
        var memberUpload = await UploadAttachmentAsync(
            projectId, task.Data.Id, Encoding.UTF8.GetBytes("member attachment"), "member.txt", "text/plain");
        Assert.Equal(HttpStatusCode.Created, memberUpload.StatusCode);
        var memberAttachment = await memberUpload.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskAttachmentResponse>>();
        Assert.NotNull(memberAttachment?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        var ownerUpload = await UploadAttachmentAsync(
            projectId, task.Data.Id, Encoding.UTF8.GetBytes("owner attachment"), "owner.txt", "text/plain");
        ownerUpload.EnsureSuccessStatusCode();
        var ownerAttachment = await ownerUpload.Content.ReadFromJsonAsync<ApiResponse<ProjectTaskAttachmentResponse>>();
        Assert.NotNull(ownerAttachment?.Data);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberTokens.AccessToken);
        var memberCannotDeleteOwnerAttachment = await _client.DeleteAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{ownerAttachment.Data.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, memberCannotDeleteOwnerAttachment.StatusCode);

        var memberDelete = await _client.DeleteAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{memberAttachment.Data.Id}");
        memberDelete.EnsureSuccessStatusCode();

        var viewerTokens = await LoginAsync("attachments.roles-viewer@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerTokens.AccessToken);
        var viewerDownload = await _client.GetAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{ownerAttachment.Data.Id}/download");
        viewerDownload.EnsureSuccessStatusCode();
        var viewerUpload = await UploadAttachmentAsync(
            projectId, task.Data.Id, Encoding.UTF8.GetBytes("viewer attachment"), "viewer.txt", "text/plain");
        Assert.Equal(HttpStatusCode.Forbidden, viewerUpload.StatusCode);

        var outsiderTokens = await LoginAsync("attachments.roles-outsider@example.com", "password123");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        var outsiderList = await _client.GetAsync($"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments");
        Assert.Equal(HttpStatusCode.NotFound, outsiderList.StatusCode);
        var outsiderDownload = await _client.GetAsync(
            $"/api/projects/{projectId}/tasks/{task.Data.Id}/attachments/{ownerAttachment.Data.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, outsiderDownload.StatusCode);
    }

    [Fact]
    public async Task Deadline_reminder_processor_notifies_assignees_once_for_upcoming_and_overdue_tasks()
    {
        var ownerId = await SeedUserAsync("reminders.owner@example.com", "password123", "Reminder Owner");
        var assigneeId = await SeedUserAsync("reminders.assignee@example.com", "password123", "Reminder Assignee");
        var projectId = await SeedProjectAsync(ownerId, "Reminder project");
        await SeedProjectMemberAsync(projectId, assigneeId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.NotificationEmailPreferences.Add(new NotificationEmailPreference
        {
            UserId = assigneeId,
            IsEmailEnabled = true,
            IsTaskDeadlineReminderEmailEnabled = false,
            UpdatedAt = DateTime.UtcNow
        });
        var upcomingTask = ProjectTask.Create(
            projectId, "Upcoming deadline", null, ProjectTaskPriority.Normal, DateTime.UtcNow.AddHours(12), assigneeId, ownerId);
        var overdueTask = ProjectTask.Create(
            projectId, "Overdue task", null, ProjectTaskPriority.Normal, DateTime.UtcNow.AddHours(-2), assigneeId, ownerId);
        var completedTask = ProjectTask.Create(
            projectId, "Completed task", null, ProjectTaskPriority.Normal, DateTime.UtcNow.AddHours(6), assigneeId, ownerId);
        completedTask.ChangeStatus(ProjectTaskStatus.Done);

        dbContext.ProjectTasks.AddRange(upcomingTask, overdueTask, completedTask);
        await dbContext.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<IProjectTaskDeadlineReminderService>();
        await processor.ProcessDueTasksAsync();
        await processor.ProcessDueTasksAsync();

        var notifications = await dbContext.Notifications
            .Where(notification => notification.UserId == assigneeId)
            .ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, notification => notification.Type == NotificationType.TaskDeadlineApproaching);
        Assert.Contains(notifications, notification => notification.Type == NotificationType.TaskOverdue);
        Assert.All(notifications, notification => Assert.Equal(projectId, notification.ProjectId));
        Assert.Equal(2, await dbContext.ProjectTaskDeadlineReminders.CountAsync());
        Assert.DoesNotContain(await dbContext.NotificationEmailOutboxMessages.ToListAsync(), message => message.UserId == assigneeId);
    }

    private async Task<HttpResponseMessage> UploadAttachmentAsync(
        Guid projectId,
        Guid taskId,
        byte[] bytes,
        string fileName,
        string contentType)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return await _client.PostAsync($"/api/projects/{projectId}/tasks/{taskId}/attachments", form);
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
        var project = Project.Create(ownerId, name);

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    private async Task SeedProjectMemberAsync(Guid projectId, Guid userId, ProjectMemberRole role = ProjectMemberRole.Member)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ProjectMembers.Add(ProjectMember.Create(projectId, userId, role));
        await dbContext.SaveChangesAsync();
    }
}