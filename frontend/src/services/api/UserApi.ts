import type {
  UpdateUserRequest,
  UpdateUserResponse,
  GetUserSecurityResponse,
  UpdateTwoFactorPreferenceRequest,
  UpdateTwoFactorPreferenceResponse,
} from '../../types';
import { httpClient, type HttpClient } from './HttpClient';

export class UserApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  updateMe(request: UpdateUserRequest): Promise<UpdateUserResponse> {
    return this.client.put<UpdateUserResponse, UpdateUserRequest>('/users/me', request);
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