/**
 * USER TYPES - DTOs for user CRUD endpoints.
 *
 * These map to backend Application.DTOs.User.UserDto and related request/response shapes.
 */

export interface UserDto {
  id: string;
  firstName: string;
  lastName: string;
  displayName?: string;
  email: string;
  avatarUrl?: string | null;
  role?: 'User' | 'Admin';
  phoneNumber: string;
  address: string;
  createdAt: string;
}

export interface CreateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  address: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  avatarUrl?: string | null;
}

export interface DeleteUserRequest {
  userId: string;
}

export interface ErrorDetail {
  field?: string;
  message: string;
  code?: string;
}

export interface GetUserResponse {
  statusCode: number;
  message: string;
  data: UserDto;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface GetAllUsersResponse {
  statusCode: number;
  message: string;
  data: UserDto[];
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface GetUserCountResponse {
  statusCode: number;
  message: string;
  data: number;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface CreateUserResponse {
  statusCode: number;
  message: string;
  data: UserDto;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UpdateUserResponse {
  statusCode: number;
  message: string;
  data: UserDto;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UpdateDisplayNameResponse {
  statusCode: number;
  message: string;
  data: UserDto;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UpdateUserRoleResponse {
  statusCode: number;
  message: string;
  data: UserDto;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

/** Account security snapshot - maps to Application.DTOs.User.UserSecurityDto. */
export interface UserSecurity {
  email: string;
  isEmailConfirmed: boolean;
  isTwoFactorEnabled: boolean;
}

/** Maps to Application.DTOs.User.UpdateTwoFactorPreferenceDto. */
export interface UpdateTwoFactorPreferenceRequest {
  enable: boolean;
}

export interface GetUserSecurityResponse {
  statusCode: number;
  message: string;
  data: UserSecurity;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UpdateTwoFactorPreferenceResponse {
  statusCode: number;
  message: string;
  data: UserSecurity;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface DeleteUserResponse {
  statusCode: number;
  message: string;
  data: null;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface PaginatedResponse<T> {
  statusCode: number;
  message: string;
  data: {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
  };
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UserListState {
  users: UserDto[];
  loading: boolean;
  error: string | null;
  selectedUser: UserDto | null;
}

export interface UserFormState {
  loading: boolean;
  error: string | null;
  success: boolean;
  successMessage: string | null;
}

export interface UserSecurity {
  email: string;
  isEmailConfirmed: boolean;
  hasTwoFactorEnabled: boolean;
}

export interface UpdateTwoFactorPreferenceRequest {
  enable: boolean;
}

export interface GetUserSecurityResponse {
  statusCode: number;
  message: string;
  data: UserSecurity;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

export interface UpdateTwoFactorPreferenceResponse {
  statusCode: number;
  message: string;
  data: UserSecurity;
  errors: ErrorDetail[] | null;
  timestamp: string;
} 
