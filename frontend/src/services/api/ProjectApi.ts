import type {
  CreateProjectRequest,
  CreateProjectTaskRequest,
  CreateProjectTaskCommentRequest,
  CreateProjectInvitationRequest,
  ProjectResponse,
  ProjectOperationResponse,
  ProjectMembersResponse,
  ProjectMemberUsersResponse,
  ProjectMemberResponse,
  ProjectActivitiesResponse,
  ProjectDashboardResponse,
  ProjectTaskResponse,
  ProjectTaskCommentResponse,
  ProjectTaskCommentsResponse,
  ProjectTaskAttachmentResponse,
  ProjectTaskAttachmentsResponse,
  ProjectTaskQuery,
  ProjectInvitationsResponse,
  CreatedProjectInvitationResponse,
  ProjectInvitationResponse,
  ProjectTasksResponse,
  ProjectsResponse,
  UpdateProjectRequest,
  UpdateProjectTaskRequest,
  UpdateProjectTaskStatusRequest,
  ProjectMemberRole,
} from '../../types';
import { httpClient, type HttpClient } from './HttpClient';

export class ProjectApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  getProjects(includeArchived = false, scope = 'all'): Promise<ProjectsResponse> {
    const query = `?includeArchived=${includeArchived}&scope=${encodeURIComponent(scope)}`;
    return this.client.get<ProjectsResponse>(`/projects${query}`);
  }

  createProject(request: CreateProjectRequest): Promise<ProjectResponse> {
    return this.client.post<ProjectResponse, CreateProjectRequest>('/projects', request);
  }

  updateProject(projectId: string, request: UpdateProjectRequest): Promise<ProjectResponse> {
    return this.client.put<ProjectResponse, UpdateProjectRequest>(`/projects/${projectId}`, request);
  }

  archiveProject(projectId: string): Promise<ProjectOperationResponse> {
    return this.client.delete<ProjectOperationResponse>(`/projects/${projectId}`);
  }

  getTasks(projectId: string, request: ProjectTaskQuery = {}): Promise<ProjectTasksResponse> {
    const query = new URLSearchParams({
      pageNumber: String(request.pageNumber ?? 1),
      pageSize: String(request.pageSize ?? 20),
    });
    if (request.search?.trim()) query.set('search', request.search.trim());
    if (request.status !== undefined) query.set('status', String(request.status));
    if (request.priority !== undefined) query.set('priority', String(request.priority));
    if (request.assignedUserId) query.set('assignedUserId', request.assignedUserId);
    if (request.label?.trim()) query.set('label', request.label.trim());
    if (request.dueBefore) query.set('dueBefore', request.dueBefore);
    if (request.sortBy) query.set('sortBy', request.sortBy);
    if (request.sortDirection) query.set('sortDirection', request.sortDirection);
    return this.client.get<ProjectTasksResponse>(`/projects/${projectId}/tasks?${query.toString()}`);
  }

  getTask(projectId: string, taskId: string): Promise<ProjectTaskResponse> {
    return this.client.get<ProjectTaskResponse>(`/projects/${projectId}/tasks/${taskId}`);
  }

  createTask(projectId: string, request: CreateProjectTaskRequest): Promise<ProjectTaskResponse> {
    return this.client.post<ProjectTaskResponse, CreateProjectTaskRequest>(`/projects/${projectId}/tasks`, request);
  }

  updateTask(projectId: string, taskId: string, request: UpdateProjectTaskRequest): Promise<ProjectTaskResponse> {
    return this.client.put<ProjectTaskResponse, UpdateProjectTaskRequest>(
      `/projects/${projectId}/tasks/${taskId}`,
      request,
    );
  }

  updateTaskStatus(projectId: string, taskId: string, request: UpdateProjectTaskStatusRequest): Promise<ProjectTaskResponse> {
    return this.client.patch<ProjectTaskResponse, UpdateProjectTaskStatusRequest>(
      `/projects/${projectId}/tasks/${taskId}/status`,
      request,
    );
  }

  deleteTask(projectId: string, taskId: string, concurrencyStamp: string): Promise<ProjectOperationResponse> {
    const query = new URLSearchParams({ concurrencyStamp });
    return this.client.delete<ProjectOperationResponse>(`/projects/${projectId}/tasks/${taskId}?${query.toString()}`);
  }

  getTaskComments(projectId: string, taskId: string): Promise<ProjectTaskCommentsResponse> {
    return this.client.get<ProjectTaskCommentsResponse>(`/projects/${projectId}/tasks/${taskId}/comments`);
  }

  createTaskComment(projectId: string, taskId: string, request: CreateProjectTaskCommentRequest): Promise<ProjectTaskCommentResponse> {
    return this.client.post<ProjectTaskCommentResponse, CreateProjectTaskCommentRequest>(
      `/projects/${projectId}/tasks/${taskId}/comments`, request,
    );
  }

  deleteTaskComment(projectId: string, taskId: string, commentId: string): Promise<ProjectOperationResponse> {
    return this.client.delete<ProjectOperationResponse>(`/projects/${projectId}/tasks/${taskId}/comments/${commentId}`);
  }

  getTaskAttachments(projectId: string, taskId: string): Promise<ProjectTaskAttachmentsResponse> {
    return this.client.get<ProjectTaskAttachmentsResponse>(`/projects/${projectId}/tasks/${taskId}/attachments`);
  }

  uploadTaskAttachment(projectId: string, taskId: string, file: File): Promise<ProjectTaskAttachmentResponse> {
    const form = new FormData();
    form.append('file', file);
    return this.client.post<ProjectTaskAttachmentResponse, FormData>(
      `/projects/${projectId}/tasks/${taskId}/attachments`,
      form,
    );
  }

  downloadTaskAttachment(projectId: string, taskId: string, attachmentId: string): Promise<Blob> {
    return this.client.getBlob(`/projects/${projectId}/tasks/${taskId}/attachments/${attachmentId}/download`);
  }

  deleteTaskAttachment(projectId: string, taskId: string, attachmentId: string): Promise<ProjectOperationResponse> {
    return this.client.delete<ProjectOperationResponse>(
      `/projects/${projectId}/tasks/${taskId}/attachments/${attachmentId}`,
    );
  }

  getMembers(projectId: string): Promise<ProjectMembersResponse> {
    return this.client.get<ProjectMembersResponse>(`/projects/${projectId}/members`);
  }

  getAvailableMembers(projectId: string): Promise<ProjectMemberUsersResponse> {
    return this.client.get<ProjectMemberUsersResponse>(`/projects/${projectId}/members/available`);
  }

  getActivity(projectId: string, pageNumber = 1, pageSize = 20): Promise<ProjectActivitiesResponse> {
    return this.client.get<ProjectActivitiesResponse>(`/projects/${projectId}/activity?pageNumber=${pageNumber}&pageSize=${pageSize}`);
  }

  getDashboard(projectId: string): Promise<ProjectDashboardResponse> {
    return this.client.get<ProjectDashboardResponse>(`/projects/${projectId}/dashboard`);
  }

  addMember(projectId: string, userId: string): Promise<ProjectMemberResponse> {
    return this.client.post<ProjectMemberResponse, { userId: string }>(`/projects/${projectId}/members`, { userId });
  }

  removeMember(projectId: string, userId: string): Promise<ProjectOperationResponse> {
    return this.client.delete<ProjectOperationResponse>(`/projects/${projectId}/members/${userId}`);
  }

  updateMemberRole(projectId: string, userId: string, role: ProjectMemberRole): Promise<ProjectMemberResponse> {
    return this.client.patch<ProjectMemberResponse, { role: ProjectMemberRole }>(
      `/projects/${projectId}/members/${userId}/role`, { role });
  }

  getInvitations(projectId: string): Promise<ProjectInvitationsResponse> {
    return this.client.get<ProjectInvitationsResponse>(`/projects/${projectId}/invitations`);
  }

  createInvitation(projectId: string, request: CreateProjectInvitationRequest): Promise<CreatedProjectInvitationResponse> {
    return this.client.post<CreatedProjectInvitationResponse, CreateProjectInvitationRequest>(`/projects/${projectId}/invitations`, request);
  }

  acceptInvitation(token: string): Promise<ProjectInvitationResponse> {
    return this.client.post<ProjectInvitationResponse, { token: string }>('/project-invitations/accept', { token });
  }

  declineInvitation(token: string): Promise<ProjectInvitationResponse> {
    return this.client.post<ProjectInvitationResponse, { token: string }>('/project-invitations/decline', { token });
  }
}

export const projectApi = new ProjectApi();