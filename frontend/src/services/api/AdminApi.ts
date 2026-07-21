import type { ApiResponse } from '../../types';
import type {
  AdminDashboardStatsDto,
  AdminUserDetailsDto,
  AdminUserFilterRequestDto,
  AdminUserListItemDto,
  AdminUpdateUserRequestDto,
  AdminUserRole,
} from '../../types/admin';
import { httpClient, type HttpClient } from './HttpClient';

function appendQueryParam(searchParams: URLSearchParams, key: string, value: string | number | boolean | undefined | null) {
  if (value === undefined || value === null || value === '') {
    return;
  }

  searchParams.append(key, String(value));
}

function appendArrayQueryParam(searchParams: URLSearchParams, key: string, values: Array<string | number> | undefined) {
  if (!values || values.length === 0) {
    return;
  }

  values.forEach((value) => {
    searchParams.append(key, String(value));
  });
}

function buildUsersQuery(request: AdminUserFilterRequestDto): string {
  const searchParams = new URLSearchParams();

  appendArrayQueryParam(searchParams, 'ids', request.ids);
  appendArrayQueryParam(searchParams, 'emails', request.emails);
  appendArrayQueryParam(searchParams, 'roles', request.roles);
  appendQueryParam(searchParams, 'isActive', request.isActive);
  appendQueryParam(searchParams, 'isEmailConfirmed', request.isEmailConfirmed);
  appendQueryParam(searchParams, 'isTwoFactorEnabled', request.isTwoFactorEnabled);
  appendQueryParam(searchParams, 'pageNumber', request.pageNumber);
  appendQueryParam(searchParams, 'pageSize', request.pageSize);

  const query = searchParams.toString();
  return query ? `?${query}` : '';
}

export class AdminApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  getDashboardStats(): Promise<ApiResponse<AdminDashboardStatsDto>> {
    return this.client.get<ApiResponse<AdminDashboardStatsDto>>('/admin/dashboard-stats');
  }

  getUsers(
    request: AdminUserFilterRequestDto = { pageNumber: 1, pageSize: 10 },
  ): Promise<ApiResponse<AdminUserListItemDto[]>> {
    const query = buildUsersQuery(request);
    return this.client.get<ApiResponse<AdminUserListItemDto[]>>(`/admin/users${query}`);
  }

  getUserDetailsById(id: string): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.get<ApiResponse<AdminUserDetailsDto>>(`/admin/users/${id}`);
  }

  getUserDetailsByEmail(email: string): Promise<ApiResponse<AdminUserDetailsDto>> {
    const query = new URLSearchParams({ email }).toString();
    return this.client.get<ApiResponse<AdminUserDetailsDto>>(`/admin/users/by-email?${query}`);
  }

  updateUser(id: string, request: AdminUpdateUserRequestDto): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.put<ApiResponse<AdminUserDetailsDto>, AdminUpdateUserRequestDto>(`/admin/users/${id}`, request);
  }

  updateUserRole(id: string, role: AdminUserRole): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.put<ApiResponse<AdminUserDetailsDto>, AdminUserRole>(`/admin/users/${id}/role`, role);
  }

  activateUser(id: string): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.put<ApiResponse<AdminUserDetailsDto>>(`/admin/users/${id}/activate`);
  }

  deactivateUser(id: string): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.put<ApiResponse<AdminUserDetailsDto>>(`/admin/users/${id}/deactivate`);
  }

  deleteUser(id: string): Promise<ApiResponse<AdminUserDetailsDto>> {
    return this.client.delete<ApiResponse<AdminUserDetailsDto>>(`/admin/users/${id}`);
  }
}

export const adminApi = new AdminApi();