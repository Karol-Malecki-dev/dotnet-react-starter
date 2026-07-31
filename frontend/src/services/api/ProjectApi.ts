import type {
  CreateProjectRequest,
  CreateProjectTaskRequest,
  ProjectResponse,
  ProjectOperationResponse,
  ProjectMembersResponse,
  ProjectMemberUsersResponse,
  ProjectMemberResponse,
  ProjectActivitiesResponse,
  ProjectTaskResponse,
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

  getTasks(projectId: string, pageNumber = 1, pageSize = 20, search = ''): Promise<ProjectTasksResponse> {
    const query = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) });
    if (search.trim()) query.set('search', search.trim());
    return this.client.get<ProjectTasksResponse>(`/projects/${projectId}/tasks?${query.toString()}`);
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

  deleteTask(projectId: string, taskId: string): Promise<ProjectOperationResponse> {
    return this.client.delete<ProjectOperationResponse>(`/projects/${projectId}/tasks/${taskId}`);
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
}

export const projectApi = new ProjectApi();