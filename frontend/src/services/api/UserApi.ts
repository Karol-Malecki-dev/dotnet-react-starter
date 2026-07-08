import type {
  ApiResponse,
  GetAllUsersResponse,
  GetUserCountResponse,
  GetUserResponse,
  UpdateDisplayNameResponse,
  UpdateUserRequest,
  UpdateUserResponse,
  UpdateUserRoleResponse,
  GetUserSecurityResponse,
  UpdateTwoFactorPreferenceRequest,
  UpdateTwoFactorPreferenceResponse,
} from '../../types';
import { httpClient, type HttpClient } from './HttpClient';

export class UserApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  getUsers(pageNumber = 1, pageSize = 10): Promise<GetAllUsersResponse> {
    return this.client.get<GetAllUsersResponse>(`/users?pageNumber=${pageNumber}&pageSize=${pageSize}`);
  }

  getUser(id: string): Promise<GetUserResponse> {
    return this.client.get<GetUserResponse>(`/users/${id}`);
  }

  getUserCount(): Promise<GetUserCountResponse> {
    return this.client.get<GetUserCountResponse>('/users/count');
  }

  updateMe(request: UpdateUserRequest): Promise<UpdateUserResponse> {
    return this.client.put<UpdateUserResponse, UpdateUserRequest>('/users/me', request);
  }

  updateDisplayName(id: string, displayName: string): Promise<UpdateDisplayNameResponse> {
    return this.client.put<UpdateDisplayNameResponse, string>(`/users/${id}/display-name`, displayName);
  }

  updateUserRole(id: string, role: 'User' | 'Admin'): Promise<UpdateUserRoleResponse> {
    return this.client.put<UpdateUserRoleResponse, string>(`/users/${id}/role`, role);
  }

  deleteUser(id: string): Promise<ApiResponse<null>> {
    return this.client.delete<ApiResponse<null>>(`/users/${id}`);
  }

  getUserSecurity(): Promise<GetUserSecurityResponse> {
    return this.client.get<GetUserSecurityResponse>(`/users/me/security`);
  }

  updateTwoFactorPreference(request: UpdateTwoFactorPreferenceRequest): Promise<UpdateTwoFactorPreferenceResponse> {
    return this.client.patch<UpdateTwoFactorPreferenceResponse, UpdateTwoFactorPreferenceRequest>(
      '/users/me/security/two-factor',
      request,
    );
  }


}

export const userApi = new UserApi();