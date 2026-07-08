import type {
  ApiResponse,
  AuthUser,
  ChangePasswordRequest,
  ConfirmEmailRequest,
  JwtTokens,
  LoginResponseData,
  LoginRequest,
  MeResponse,
  RegisterRequest,
  RegisterResultData,
  ResendConfirmationRequest,
  ResendTwoFactorRequest,
  TwoFactorChallenge,
  VerifyTwoFactorRequest,
  VerifyTokenRequest,
  VerifyTokenResponse,
} from '../../types';
import { httpClient, type HttpClient } from './HttpClient';

export class AuthApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  login(request: LoginRequest): Promise<ApiResponse<LoginResponseData>> {
    return this.client.post<ApiResponse<LoginResponseData>, LoginRequest>('/auth/login', request, {
      skipAuth: true,
    });
  }

  register(request: RegisterRequest): Promise<ApiResponse<RegisterResultData>> {
    return this.client.post<ApiResponse<RegisterResultData>, RegisterRequest>('/auth/register', request, {
      skipAuth: true,
    });
  }

  confirmEmail(request: ConfirmEmailRequest): Promise<ApiResponse<null>> {
    return this.client.post<ApiResponse<null>, ConfirmEmailRequest>('/auth/confirm-email', request, {
      skipAuth: true,
    });
  }

  resendConfirmation(request: ResendConfirmationRequest): Promise<ApiResponse<null>> {
    return this.client.post<ApiResponse<null>, ResendConfirmationRequest>('/auth/resend-confirmation', request, {
      skipAuth: true,
    });
  }

  verifyTwoFactor(request: VerifyTwoFactorRequest): Promise<ApiResponse<JwtTokens>> {
    return this.client.post<ApiResponse<JwtTokens>, VerifyTwoFactorRequest>('/auth/verify-2fa', request, {
      skipAuth: true,
    });
  }

  resendTwoFactor(request: ResendTwoFactorRequest): Promise<ApiResponse<TwoFactorChallenge>> {
    return this.client.post<ApiResponse<TwoFactorChallenge>, ResendTwoFactorRequest>('/auth/resend-2fa', request, {
      skipAuth: true,
    });
  }

  refreshToken(): Promise<ApiResponse<JwtTokens>> {
    return this.client.post<ApiResponse<JwtTokens>, undefined>('/auth/refresh-token', undefined, {
      skipAuth: true,
    });
  }

  logout(): Promise<ApiResponse<null>> {
    return this.client.post<ApiResponse<null>, undefined>('/auth/logout');
  }

  me(): Promise<ApiResponse<AuthUser>> {
    return this.client.get<MeResponse>('/auth/me');
  }

  changePassword(request: ChangePasswordRequest): Promise<ApiResponse<null>> {
    return this.client.post<ApiResponse<null>, ChangePasswordRequest>('/auth/change-password', request);
  }

  verifyToken(request: VerifyTokenRequest): Promise<ApiResponse<VerifyTokenResponse>> {
    return this.client.post<ApiResponse<VerifyTokenResponse>, VerifyTokenRequest>('/auth/verify-token', request, {
      skipAuth: true,
    });
  }
}

export const authApi = new AuthApi();