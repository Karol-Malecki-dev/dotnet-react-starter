/**
 * AUTH TYPES - Mapowanie z backendu C# DTOs
 * 
 * C# → TypeScript Mapping Rules:
 * - Guid → string (UUID format)
 * - DateTime → string (ISO 8601 format)
 * - UserRole enum → 'User' | 'Admin'
 * - string? (nullable) → string | null
 * - long → number
 */

// ============================================
// 1️⃣ REQUESTS (Frontend → Backend)
// ============================================

/** Login request - maps to LoginUserDto.cs */
export interface LoginRequest {
  email: string;
  password: string;
}

/** Register request - maps to RegisterUserDto.cs */
export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phoneNumber: string;
  address: string;
}

/** Verify token request */
export interface VerifyTokenRequest {
  token: string;
}

export interface ConfirmEmailRequest {
  userId: string;
  token: string;
}

export interface ResendConfirmationRequest {
  email: string;
}

export interface VerifyTwoFactorRequest {
  challengeId: string;
  code: string;
}

export interface ResendTwoFactorRequest {
  challengeId: string;
}

/** Mirrors backend Domain.Enums.Auth.ResetType. Serialized as its numeric value (no string enum converter on backend). */
export enum ResetType {
  Link = 0,
  Code = 1,
}

/** Forgot password request - maps to ForgotPasswordRequestDto.cs. Backend currently only accepts ResetType.Link. */
export interface ForgotPasswordRequest {
  email: string;
  resetType: ResetType;
}

/** Reset password request - maps to ResetPasswordRequestDto.cs. token is required for the link-based flow. */
export interface ResetPasswordRequest {
  email: string;
  resetType: ResetType;
  token?: string;
  code?: string;
  newPassword: string;
}

// ============================================
// 2️⃣ RESPONSES (Backend → Frontend)
// ============================================

/** Public auth token response returned by the backend. */
export interface JwtTokens {
  accessToken: string;
  expiresIn: number; // seconds (900 = 15 minutes)
  tokenType?: string;
}

export interface RegisterResultData {
  email: string;
  requiresEmailConfirmation: boolean;
}

export interface TwoFactorChallenge {
  requiresTwoFactor: true;
  challengeId: string;
  destinationHint: string;
  expiresAt: string;
}

export type LoginResponseData = JwtTokens | TwoFactorChallenge;

export interface LoginAuthenticatedResult {
  kind: 'authenticated';
}

export interface LoginTwoFactorRequiredResult {
  kind: 'two-factor-required';
  challenge: TwoFactorChallenge;
}

export type LoginFlowResult = LoginAuthenticatedResult | LoginTwoFactorRequiredResult;

export interface PendingTwoFactorChallenge extends TwoFactorChallenge {
  fromPath?: string;
}

/** Current authenticated user - maps to /api/auth/me response */
export interface AuthUser {
  id: string; // Guid → UUID string
  email: string;
  displayName: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string | null;
  role: 'User' | 'Admin'; // UserRole enum
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/** Login response from backend */
export interface LoginResponse {
  statusCode: number;
  message: string;
  data: LoginResponseData;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

/** Register response from backend */
export interface RegisterResponse {
  statusCode: number;
  message: string;
  data: RegisterResultData;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

/** Get current user (Me) response */
export interface MeResponse {
  statusCode: number;
  message: string;
  data: AuthUser;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

/** Verify token response */
export interface VerifyTokenResponse {
  isValid: boolean;
  expiresAt?: string;
}

/** Logout response */
export interface LogoutResponse {
  statusCode: number;
  message: string;
  data: null;
  errors: ErrorDetail[] | null;
  timestamp: string;
}

// ============================================
// 3️⃣ ERROR HANDLING
// ============================================

/** Error detail from ApiResponse - maps to Shared/ErrorDetail.cs */
export interface ErrorDetail {
  field?: string;
  message: string;
  code?: string;
}

/** Generic API error response */
export interface ApiErrorResponse {
  statusCode: number;
  message: string;
  data: null;
  errors: ErrorDetail[];
  timestamp: string;
}

// ============================================
// 4️⃣ CONTEXT STATE
// ============================================

/** Auth context state */
export interface AuthState {
  isAuthenticated: boolean;
  user: AuthUser | null;
  tokens: JwtTokens | null;
  loading: boolean;
  error: string | null;
}

/** Auth context value */
export interface AuthContextType extends AuthState {
  login: (email: string, password: string) => Promise<LoginFlowResult>;
  register: (data: RegisterRequest) => Promise<RegisterResultData>;
  verifyTwoFactor: (challengeId: string, code: string) => Promise<void>;
  resendTwoFactor: (challengeId: string) => Promise<TwoFactorChallenge>;
  logout: () => Promise<void>;
  refreshToken: () => Promise<void>;
  updateDisplayName: (displayName: string) => Promise<void>;
  updateProfile: (data: { firstName?: string; lastName?: string; email?: string; avatarUrl?: string | null }) => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  clearError: () => void;
}
