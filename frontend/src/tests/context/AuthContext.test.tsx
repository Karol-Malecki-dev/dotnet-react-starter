import { act, render, screen, waitFor } from '@testing-library/react';
import type { AuthContextType, AuthUser, JwtTokens } from '../../types';
import { AuthProvider, useAuthContext } from '../../context/AuthContext';
import { authApi, userApi } from '../../services/api';
import { tokenManager } from '../../services/api/TokenManager';

import { vi } from 'vitest';

vi.mock('../../services/api', () => ({
  authApi: {
    login: jest.fn(),
    register: jest.fn(),
    confirmEmail: jest.fn(),
    resendConfirmation: jest.fn(),
    verifyTwoFactor: jest.fn(),
    resendTwoFactor: jest.fn(),
    refreshToken: jest.fn(),
    logout: jest.fn(),
    me: jest.fn(),
    changePassword: jest.fn(),
  },
  userApi: {
    updateMe: jest.fn(),
  },
}));

vi.mock('../../services/api/TokenManager', () => ({
  tokenManager: {
    getSession: jest.fn(),
    getUser: jest.fn(),
    isAccessTokenExpired: jest.fn(),
    setSession: jest.fn(),
    clearSession: jest.fn(),
  },
}));

const mockedAuthApi = authApi as jest.Mocked<typeof authApi>;
const mockedUserApi = userApi as jest.Mocked<typeof userApi>;
const mockedTokenManager = tokenManager as jest.Mocked<typeof tokenManager>;

const session: JwtTokens = {
  accessToken: 'access-token',
  expiresIn: 900,
};

const storedUser: AuthUser = {
  id: 'user-1',
  email: 'user@example.com',
  displayName: 'Stored User',
  role: 'User',
};

let latestAuth: AuthContextType | null = null;

function AuthConsumer() {
  const auth = useAuthContext();
  latestAuth = auth;

  return (
    <>
      <div data-testid="status">{auth.loading ? 'loading' : auth.isAuthenticated ? 'authenticated' : 'anonymous'}</div>
      <div data-testid="user">{auth.user?.displayName ?? 'none'}</div>
      <div data-testid="error">{auth.error ?? 'none'}</div>
    </>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    latestAuth = null;
    jest.resetAllMocks();
  });

  it('ends bootstrap as anonymous when no stored session exists', async () => {
    mockedTokenManager.getSession.mockReturnValue(null);

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(mockedAuthApi.refreshToken).not.toHaveBeenCalled();
    expect(mockedAuthApi.me).not.toHaveBeenCalled();
  });

  it('restores an authenticated state from a valid stored session', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(false);

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));
    expect(screen.getByTestId('user')).toHaveTextContent('Stored User');
    expect(mockedAuthApi.refreshToken).not.toHaveBeenCalled();
  });

  it('refreshes an expired session during bootstrap', async () => {
    const refreshedUser: AuthUser = {
      ...storedUser,
      displayName: 'Refreshed User',
    };
    const refreshedTokens: JwtTokens = {
      accessToken: 'fresh-access-token',
      expiresIn: 900,
    };

    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(true);
    mockedAuthApi.refreshToken.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: refreshedTokens,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });
    mockedAuthApi.me.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: refreshedUser,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('Refreshed User'));
    expect(mockedAuthApi.refreshToken).toHaveBeenCalledWith();
    expect(mockedAuthApi.me).toHaveBeenCalledTimes(1);
    expect(mockedTokenManager.setSession).toHaveBeenCalledWith(refreshedTokens, storedUser);
  });

  it('clears the stored session when refresh fails during bootstrap', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(true);
    mockedAuthApi.refreshToken.mockRejectedValue(new Error('Refresh failed'));

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(mockedTokenManager.clearSession).toHaveBeenCalledTimes(1);
  });

  it('logs in and exposes the authenticated user state', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedAuthApi.login.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: session,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });
    mockedAuthApi.me.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: storedUser,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));

    await act(async () => {
      await latestAuth?.login('user@example.com', 'password123');
    });

    expect(mockedAuthApi.login).toHaveBeenCalledWith({ email: 'user@example.com', password: 'password123' });
    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('Stored User'));
  });

  it('logs out and clears the current session', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(false);
    mockedAuthApi.logout.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: null,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    await act(async () => {
      await latestAuth?.logout();
    });

    expect(mockedAuthApi.logout).toHaveBeenCalledWith();
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(mockedTokenManager.clearSession).toHaveBeenCalled();
  });

  it('updates the display name and keeps the local session in sync', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(false);
    mockedUserApi.updateMe.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: {
        id: storedUser.id,
        firstName: 'Updated',
        lastName: 'User',
        email: storedUser.email,
        displayName: 'Updated User',
        avatarUrl: null,
        role: 'User',
        phoneNumber: '+48 123 456 789',
        address: 'Warsaw',
        isEmailConfirmed: true,
        isTwoFactorEnabled: false,
        createdAt: '2026-06-26T00:00:00Z',
      },
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('Stored User'));

    await act(async () => {
      await latestAuth?.updateDisplayName('Updated User');
    });

    expect(mockedUserApi.updateMe).toHaveBeenCalledWith({ firstName: 'Updated', lastName: 'User' });
    await waitFor(() => expect(screen.getByTestId('user')).toHaveTextContent('Updated User'));
    expect(mockedTokenManager.setSession).toHaveBeenCalledWith(session, {
      ...storedUser,
      displayName: 'Updated User',
      firstName: 'Updated',
      lastName: 'User',
      avatarUrl: null,
    });
  });

  it('changes password through the auth api', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(false);
    mockedAuthApi.changePassword.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: null,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    await act(async () => {
      await latestAuth?.changePassword('password123', 'newPassword123');
    });

    expect(mockedAuthApi.changePassword).toHaveBeenCalledWith({
      currentPassword: 'password123',
      newPassword: 'newPassword123',
    });
  });

  it('clears the session when refreshToken is called and the backend refresh fails', async () => {
    mockedTokenManager.getSession.mockReturnValue(session);
    mockedTokenManager.getUser.mockReturnValue(storedUser);
    mockedTokenManager.isAccessTokenExpired.mockReturnValue(false);
    mockedAuthApi.refreshToken.mockRejectedValue(new Error('Refresh failed'));

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('authenticated'));

    let caughtErrorMessage: string | null = null;

    await act(async () => {
      try {
        await latestAuth?.refreshToken();
      } catch (error) {
        caughtErrorMessage = (error as Error).message;
      }
    });

    expect(caughtErrorMessage).toBe('Refresh failed');
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('anonymous'));
    expect(mockedTokenManager.clearSession).toHaveBeenCalled();
  });
});