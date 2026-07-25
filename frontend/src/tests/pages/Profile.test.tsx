import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import Profile from '../../pages/Profile';
import { useAuth } from '../../hooks/useAuth';
import { authApi, userApi } from '../../services/api';

jest.mock('../../hooks/useAuth');
jest.mock('../../services/api', () => ({
  authApi: {
    resendConfirmation: jest.fn(),
  },
  userApi: {
    getUserSecurity: jest.fn(),
    updateTwoFactorPreference: jest.fn(),
  },
}));
const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedAuthApi = authApi as jest.Mocked<typeof authApi>;
const mockedUserApi = userApi as jest.Mocked<typeof userApi>;

describe('Profile page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
    mockedUserApi.getUserSecurity.mockResolvedValue({
      statusCode: 200,
      message: 'Security loaded',
      data: {
        email: 'test@example.com',
        isEmailConfirmed: true,
        isTwoFactorEnabled: false,
      },
      errors: null,
      timestamp: new Date().toISOString(),
    });
  });

  it('updates profile details through the auth context', async () => {
    const updateProfile = jest.fn().mockResolvedValue(undefined);
    const changePassword = jest.fn().mockResolvedValue(undefined);

    mockedUseAuth.mockReturnValue({
      user: {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Old Name',
        firstName: 'Old',
        lastName: 'Name',
        avatarUrl: '',
        role: 'User',
      },
      tokens: { accessToken: 'access', expiresIn: 900 },
      updateProfile,
      changePassword,
    } as any);

    render(<Profile />);

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'New' } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Profile' } });
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'updated@example.com' } });
    fireEvent.change(screen.getByLabelText(/avatar url/i), { target: { value: 'https://example.com/avatar.png' } });
    fireEvent.click(screen.getByRole('button', { name: /save profile/i }));

    await waitFor(() => expect(updateProfile).toHaveBeenCalledWith({
      firstName: 'New',
      lastName: 'Profile',
      email: 'updated@example.com',
      avatarUrl: 'https://example.com/avatar.png',
    }));
    expect(await screen.findByText(/profile updated/i)).toBeInTheDocument();
  });

  it('changes password through the auth context', async () => {
    const updateProfile = jest.fn().mockResolvedValue(undefined);
    const changePassword = jest.fn().mockResolvedValue(undefined);

    mockedUseAuth.mockReturnValue({
      user: {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Old Name',
        role: 'User',
      },
      tokens: { accessToken: 'access', expiresIn: 900 },
      updateProfile,
      changePassword,
    } as any);

    render(<Profile />);

    fireEvent.change(screen.getByLabelText(/^current password$/i), { target: { value: 'password123' } });
    fireEvent.change(screen.getByLabelText(/^new password$/i), { target: { value: 'newPassword123' } });
    fireEvent.change(screen.getByLabelText(/confirm new password/i), { target: { value: 'newPassword123' } });
    fireEvent.click(screen.getByRole('button', { name: /change password/i }));

    await waitFor(() => expect(changePassword).toHaveBeenCalledWith('password123', 'newPassword123'));
    expect(await screen.findByText(/password changed/i)).toBeInTheDocument();
  });

  it('loads security settings and updates two-factor authentication', async () => {
    const updateProfile = jest.fn().mockResolvedValue(undefined);
    const changePassword = jest.fn().mockResolvedValue(undefined);
    mockedUserApi.updateTwoFactorPreference.mockResolvedValue({
      statusCode: 200,
      message: 'Two-factor authentication enabled successfully',
      data: {
        email: 'test@example.com',
        isEmailConfirmed: true,
        isTwoFactorEnabled: true,
      },
      errors: null,
      timestamp: new Date().toISOString(),
    });

    mockedUseAuth.mockReturnValue({
      user: {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Old Name',
        role: 'User',
      },
      tokens: { accessToken: 'access', expiresIn: 900 },
      updateProfile,
      changePassword,
    } as any);

    render(<Profile />);

    const twoFactorCheckbox = await screen.findByRole('checkbox', {
      name: /two-factor authentication/i,
    });
    expect(twoFactorCheckbox).not.toBeChecked();

    fireEvent.click(twoFactorCheckbox);

    await waitFor(() => {
      expect(mockedUserApi.updateTwoFactorPreference).toHaveBeenCalledWith({ enable: true });
    });
    expect(await screen.findByText(/two-factor authentication enabled successfully/i)).toBeInTheDocument();
  });

  it('resends confirmation for an unconfirmed email', async () => {
    mockedUserApi.getUserSecurity.mockResolvedValue({
      statusCode: 200,
      message: 'Security loaded',
      data: {
        email: 'test@example.com',
        isEmailConfirmed: false,
        isTwoFactorEnabled: false,
      },
      errors: null,
      timestamp: new Date().toISOString(),
    });
    mockedAuthApi.resendConfirmation.mockResolvedValue({
      statusCode: 200,
      message: 'Confirmation email sent',
      data: null,
      errors: null,
      timestamp: new Date().toISOString(),
    });

    mockedUseAuth.mockReturnValue({
      user: {
        id: 'user-1',
        email: 'test@example.com',
        displayName: 'Old Name',
        role: 'User',
      },
      tokens: { accessToken: 'access', expiresIn: 900 },
      updateProfile: jest.fn(),
      changePassword: jest.fn(),
    } as any);

    render(<Profile />);
    fireEvent.click(await screen.findByRole('button', { name: /resend confirmation/i }));

    await waitFor(() => {
      expect(mockedAuthApi.resendConfirmation).toHaveBeenCalledWith({ email: 'test@example.com' });
    });
    expect(await screen.findByText(/confirmation email sent/i)).toBeInTheDocument();
  });
});