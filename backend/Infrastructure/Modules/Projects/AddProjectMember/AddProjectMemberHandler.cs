using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.AddProjectMember;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.AddProjectMember;

/// <summary>
/// Coordinates the add-project-member command without exposing persistence details to the API.
/// </summary>
public sealed class AddProjectMemberHandler : IRequestHandler<AddProjectMemberCommand, ProjectOperationResult<ProjectMemberView>>
{
    private readonly IAddProjectMemberStore _store;
    private readonly IAddProjectMemberNotificationWriter _notificationWriter;

    public AddProjectMemberHandler(
        IAddProjectMemberStore store,
        IAddProjectMemberNotificationWriter notificationWriter)
    {
        _store = store;
        _notificationWriter = notificationWriter;
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> Handle(
        AddProjectMemberCommand request,
        CancellationToken cancellationToken = default)
    {
        var project = await _store.GetOwnedProjectWithMembersAsync(
            request.OwnerId,
            request.ProjectId,
            cancellationToken);

        if (project is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project not found");
        }

        var user = await _store.GetActiveUserAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.NotFound,
                "User not found or inactive");
        }

        if (await _store.IsMemberAsync(request.ProjectId, request.UserId, cancellationToken))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "User is already a project member");
        }

        ProjectMember member;
        try
        {
            member = project.AddMember(request.UserId);
        }
        catch (InvalidOperationException)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "User is already a project member");
        }

        _store.AddMember(member);
        _store.AddActivity(new ProjectActivity
        {
            ProjectId = request.ProjectId,
            ActorUserId = request.OwnerId,
            Type = "member.added",
            Description = $"added {user.DisplayName.Value} to the project."
        });

        await _notificationWriter.AddProjectMemberNotificationAsync(
            user.Id,
            request.ProjectId,
            project.Name,
            cancellationToken);

        try
        {
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            PostgreSqlErrorClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_ProjectMembers_ProjectId_UserId"))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(
                ProjectOperationStatus.Conflict,
                "User is already a project member");
        }

        return ProjectOperationResult<ProjectMemberView>.Success(
            new ProjectMemberView(
                user.Id,
                user.DisplayName.Value,
                user.Email.Value,
                member.Role,
                member.AddedAt),
            "Project member added",
            201);
    }
}
