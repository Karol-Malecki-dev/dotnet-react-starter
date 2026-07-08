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
} from './auth';

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
} from './user/index';

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
