import type { ApiResponse } from './api';

export enum ProjectTaskStatus {
  Todo = 1,
  InProgress = 2,
  Done = 3,
}

export enum ProjectTaskPriority {
  Low = 1,
  Normal = 2,
  High = 3,
}

export enum ProjectMemberRole {
  Owner = 1,
  Member = 2,
  Viewer = 3,
}

export interface ProjectDto {
  id: string;
  name: string;
  description: string | null;
  ownerId: string;
  createdAt: string;
  updatedAt: string;
  isArchived: boolean;
  currentUserRole?: ProjectMemberRole;
}

export interface ProjectTaskDto {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: ProjectTaskStatus;
  priority: ProjectTaskPriority;
  dueDate: string | null;
  assignedUserId: string | null;
  createdByUserId?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ProjectMemberDto {
  userId: string;
  displayName: string;
  email: string;
  addedAt: string;
  role?: ProjectMemberRole;
}

export interface ProjectMemberUserDto {
  id: string;
  displayName: string;
  email: string;
}

export interface ProjectActivityDto {
  id: string;
  type: string;
  description: string;
  actorUserId: string;
  actorDisplayName: string;
  projectTaskId: string | null;
  createdAt: string;
}

export interface CreateProjectRequest {
  name: string;
  description?: string;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string;
}

export interface CreateProjectTaskRequest {
  title: string;
  description?: string;
  priority: ProjectTaskPriority;
  dueDate?: string;
  assignedUserId?: string;
}

export interface UpdateProjectTaskRequest extends CreateProjectTaskRequest {}

export interface UpdateProjectTaskStatusRequest {
  status: ProjectTaskStatus;
}

export type ProjectsResponse = ApiResponse<ProjectDto[]>;
export type ProjectResponse = ApiResponse<ProjectDto>;
export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type ProjectTasksResponse = ApiResponse<PagedResult<ProjectTaskDto>>;
export type ProjectTaskResponse = ApiResponse<ProjectTaskDto>;
export type ProjectOperationResponse = ApiResponse<boolean>;
export type ProjectMembersResponse = ApiResponse<ProjectMemberDto[]>;
export type ProjectMemberUsersResponse = ApiResponse<ProjectMemberUserDto[]>;
export type ProjectMemberResponse = ApiResponse<ProjectMemberDto>;
export type ProjectActivitiesResponse = ApiResponse<PagedResult<ProjectActivityDto>>;