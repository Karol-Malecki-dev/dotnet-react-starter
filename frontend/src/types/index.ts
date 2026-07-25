/**
 * TYPES INDEX - Central export point for all types
 * 
 * Usage:
 * import { User, LoginRequest, ApiResponse } from '@/types';
 */

// Auth types
export type {
  LoginRequest,
  RegisterRequest,
  VerifyTokenRequest,
  ConfirmEmailRequest,
  ResendConfirmationRequest,
  VerifyTwoFactorRequest,
  ResendTwoFactorRequest,
  JwtTokens,
  RegisterResultData,
  TwoFactorChallenge,
  LoginResponseData,
  LoginFlowResult,
  LoginAuthenticatedResult,
  LoginTwoFactorRequiredResult,
  PendingTwoFactorChallenge,
  AuthUser,
  ChangePasswordRequest,
  LoginResponse,
  RegisterResponse,
  MeResponse,
  VerifyTokenResponse,
  LogoutResponse,
  ErrorDetail,
  ApiErrorResponse,
  AuthState,
  AuthContextType,
  ForgotPasswordRequest,
  ResetPasswordRequest,
} from './auth';

// Runtime config types
export type {
  AppFeatureFlagsDto,
  AppRuntimeConfigurationDto,
} from './runtimeConfig';

// ResetType is a real runtime enum (not just a type), so it must be exported separately
// from the `export type { ... }` block above (isolatedModules forbids mixing them).
export { ResetType } from './auth';

// User types
export type {
  UserDto,
  CreateUserRequest,
  UpdateUserRequest,
  DeleteUserRequest,
  GetUserResponse,
  GetAllUsersResponse,
  GetUserCountResponse,
  CreateUserResponse,
  UpdateUserResponse,
  UpdateDisplayNameResponse,
  UpdateUserRoleResponse,
  DeleteUserResponse,
  PaginatedResponse,
  UserListState,
  UserFormState,
  UserSecurity,
  UpdateTwoFactorPreferenceRequest,
  GetUserSecurityResponse,
  UpdateTwoFactorPreferenceResponse,
} from './user/index';

// Admin types
export type {
  AdminDashboardStatsDto,
  AdminUserListItemDto,
  AdminUserDetailsDto,
  AdminUserFilterRequestDto,
  AdminUpdateUserRequestDto,
} from './admin';

export { AdminUserRole } from './admin';

// API types
export type {
  ApiResponse,
  ApiError,
  AsyncRequest,
  PaginatedRequest,
  ValidationRule,
  ValidationRules,
  FormErrors,
  AxiosErrorResponse,
} from './api';

export { HttpStatusCode } from './api';

// Project management types
export {
  ProjectTaskStatus,
  ProjectTaskPriority,
  ProjectMemberRole,
} from './project';

export type {
  ProjectDto,
  ProjectTaskDto,
  ProjectMemberDto,
  ProjectMemberUserDto,
  CreateProjectRequest,
  UpdateProjectRequest,
  CreateProjectTaskRequest,
  UpdateProjectTaskRequest,
  UpdateProjectTaskStatusRequest,
  ProjectsResponse,
  ProjectResponse,
  ProjectTasksResponse,
  ProjectTaskResponse,
  ProjectOperationResponse,
  ProjectMembersResponse,
  ProjectMemberUsersResponse,
  ProjectMemberResponse,
} from './project';

