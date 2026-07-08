import React, { createContext, useContext, useEffect, useState } from 'react';
import type {
  AuthContextType,
  AuthState,
  AuthUser,
  JwtTokens,
  LoginFlowResult,
  RegisterRequest,
  RegisterResultData,
  TwoFactorChallenge,
  UpdateUserRequest,
  UserDto,
} from '../types';
import { authApi } from '../services/api';
import { userApi } from '../services/api';
import { tokenManager } from '../services/api/TokenManager';

function buildDisplayName(firstName?: string, lastName?: string) {
  return [firstName?.trim(), lastName?.trim()].filter(Boolean).join(' ').trim();
}

function splitDisplayName(displayName: string) {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  return {
    firstName: parts[0] ?? '',
    lastName: parts.slice(1).join(' '),
  };
}

function mapUserDtoToAuthUser(user: UserDto, fallbackRole: AuthUser['role']): AuthUser {
  const displayName = user.displayName?.trim() || buildDisplayName(user.firstName, user.lastName);

  return {
    id: user.id,
    email: user.email,
    displayName: displayName || 'User',
    firstName: user.firstName,
    lastName: user.lastName,
    avatarUrl: user.avatarUrl ?? null,
    role: user.role ?? fallbackRole,
  };
}

function isTwoFactorChallenge(value: unknown): value is TwoFactorChallenge {
  return Boolean(
    value
      && typeof value === 'object'
      && 'requiresTwoFactor' in value
      && (value as { requiresTwoFactor?: unknown }).requiresTwoFactor === true,
  );
}

const initialState: AuthState = {
  isAuthenticated: false,
  user: null,
  tokens: null,
  loading: true,
  error: null,
};

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>(initialState);

  useEffect(() => {
    let isMounted = true;

    const bootstrap = async () => {
      const session = tokenManager.getSession();

      if (!session) {
        if (isMounted) {
          setState((current) => ({ ...current, loading: false }));
        }
        return;
      }

      const storedUser = tokenManager.getUser();

      if (!tokenManager.isAccessTokenExpired() && storedUser) {
        if (isMounted) {
          setState({
            isAuthenticated: true,
            user: storedUser,
            tokens: session,
            loading: false,
            error: null,
          });
        }
        return;
      }

      try {
        const refreshed = await authApi.refreshToken();
        if (!refreshed.data) {
          throw new Error('Refresh response missing token payload');
        }

        tokenManager.setSession(refreshed.data, storedUser ?? null);

        const currentUserResponse = await authApi.me();
        if (isMounted) {
          setState({
            isAuthenticated: true,
            user: currentUserResponse.data,
            tokens: refreshed.data,
            loading: false,
            error: null,
          });
        }
      } catch {
        tokenManager.clearSession();
        if (isMounted) {
          setState({ ...initialState, loading: false });
        }
      }
    };

    void bootstrap();

    return () => {
      isMounted = false;
    };
  }, []);

  const persistSession = (tokens: JwtTokens, user: AuthUser) => {
    tokenManager.setSession(tokens, user);
    setState({
      isAuthenticated: true,
      user,
      tokens,
      loading: false,
      error: null,
    });
  };

  const clearSession = () => {
    tokenManager.clearSession();
    setState({ ...initialState, loading: false });
  };

  const loadCurrentUser = async (): Promise<AuthUser> => {
    const response = await authApi.me();
    if (!response.data) {
      throw new Error('Current user payload missing');
    }

    const currentTokens = tokenManager.getSession();
    if (!currentTokens) {
      throw new Error('Session token missing');
    }

    tokenManager.setSession(currentTokens, response.data);
    return response.data;
  };

  const login = async (email: string, password: string): Promise<LoginFlowResult> => {
    setState((current) => ({ ...current, loading: true, error: null }));

    try {
      const response = await authApi.login({ email, password });
      if (!response.data) {
        throw new Error('Login response missing payload');
      }

      if (isTwoFactorChallenge(response.data)) {
        clearSession();
        setState((current) => ({
          ...current,
          isAuthenticated: false,
          user: null,
          tokens: null,
          loading: false,
          error: null,
        }));

        return {
          kind: 'two-factor-required',
          challenge: response.data,
        };
      }

      tokenManager.setSession(response.data);
      const currentUser = await loadCurrentUser();
      persistSession(response.data, currentUser);

      return { kind: 'authenticated' };
    } catch (error) {
      clearSession();
      setState((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : 'Login failed',
      }));
      throw error;
    }
  };

  const register = async (request: RegisterRequest): Promise<RegisterResultData> => {
    setState((current) => ({ ...current, loading: true, error: null }));

    try {
      const response = await authApi.register(request);
      if (!response.data) {
        throw new Error('Register response missing payload');
      }

      setState((current) => ({
        ...current,
        loading: false,
        error: null,
      }));

      return response.data;
    } catch (error) {
      setState((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : 'Registration failed',
      }));
      throw error;
    }
  };

  const verifyTwoFactor = async (challengeId: string, code: string) => {
    setState((current) => ({ ...current, loading: true, error: null }));

    try {
      const response = await authApi.verifyTwoFactor({ challengeId, code });
      if (!response.data) {
        throw new Error('Two-factor verification response missing tokens');
      }

      tokenManager.setSession(response.data);
      const currentUser = await loadCurrentUser();
      persistSession(response.data, currentUser);
    } catch (error) {
      clearSession();
      setState((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : 'Two-factor verification failed',
      }));
      throw error;
    }
  };

  const resendTwoFactor = async (challengeId: string): Promise<TwoFactorChallenge> => {
    setState((current) => ({ ...current, loading: true, error: null }));

    try {
      const response = await authApi.resendTwoFactor({ challengeId });
      if (!response.data) {
        throw new Error('Two-factor challenge payload missing');
      }

      setState((current) => ({
        ...current,
        loading: false,
        error: null,
      }));

      return response.data;
    } catch (error) {
      setState((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : 'Unable to resend the verification code',
      }));
      throw error;
    }
  };

  const refreshToken = async () => {
    const currentTokens = tokenManager.getSession();
    if (!currentTokens) {
      clearSession();
      throw new Error('Access token not available');
    }

    try {
      const response = await authApi.refreshToken();
      if (!response.data) {
        throw new Error('Refresh response missing tokens');
      }

      const nextUser = tokenManager.getUser();
      tokenManager.setSession(response.data, nextUser ?? null);
      setState((current) => ({
        ...current,
        isAuthenticated: Boolean(nextUser),
        tokens: response.data,
        user: nextUser,
        loading: false,
        error: null,
      }));
    } catch (error) {
      clearSession();
      setState((current) => ({
        ...current,
        loading: false,
        error: error instanceof Error ? error.message : 'Session refresh failed',
      }));
      throw error;
    }
  };

  const logout = async () => {
    try {
      if (tokenManager.getSession()) {
        await authApi.logout();
      }
    } finally {
      clearSession();
    }
  };

  const updateProfile = async (request: UpdateUserRequest) => {
    const currentUser = state.user;
    const currentTokens = tokenManager.getSession();

    if (!currentUser || !currentTokens) {
      throw new Error('Authenticated user is required');
    }

    const response = await userApi.updateMe(request);
    if (!response.data) {
      throw new Error('Profile update response missing user payload');
    }

    const nextUser = mapUserDtoToAuthUser(response.data, currentUser.role);
    tokenManager.setSession(currentTokens, nextUser);
    setState((current) => ({
      ...current,
      user: nextUser,
      tokens: currentTokens,
      error: null,
    }));
  };

  const updateDisplayName = async (displayName: string) => {
    const { firstName, lastName } = splitDisplayName(displayName);
    await updateProfile({ firstName, lastName });
  };

  const changePassword = async (currentPassword: string, newPassword: string) => {
    await authApi.changePassword({ currentPassword, newPassword });
  };

  const clearError = () => setState((current) => ({ ...current, error: null }));

  const value: AuthContextType = {
    ...state,
    login,
    register,
    verifyTwoFactor,
    resendTwoFactor,
    logout,
    refreshToken,
    updateDisplayName,
    updateProfile,
    changePassword,
    clearError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthContextType {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuthContext must be used within AuthProvider');
  }

  return context;
}
