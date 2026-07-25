using Application.DTOs.Project;
using Domain.Enums;
using Shared.Responses;

namespace Application.Interfaces;

public interface IProjectService
{
    Task<ApiResponse<List<ProjectDto>>> GetUserProjectsAsync(Guid ownerId, bool includeArchived = false, string scope = "all");
    Task<ApiResponse<ProjectDto>> GetProjectAsync(Guid ownerId, Guid projectId, bool includeArchived = false);
    Task<ApiResponse<ProjectDto>> CreateProjectAsync(Guid ownerId, CreateProjectDto dto);
    Task<ApiResponse<ProjectDto>> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectDto dto);
    Task<ApiResponse<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId);
    Task<ApiResponse<List<ProjectMemberDto>>> GetProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ApiResponse<List<ProjectMemberUserDto>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId);
    Task<ApiResponse<ProjectMemberDto>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
    Task<ApiResponse<ProjectMemberDto>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, ProjectMemberRole role);
    Task<ApiResponse<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId);
}