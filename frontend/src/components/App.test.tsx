import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import App from '../App';
import { useAuth } from '../hooks/useAuth';

jest.mock('../hooks/useAuth');
jest.mock('../context/RuntimeConfigContext', () => ({
  RuntimeConfigProvider: ({ children }: { children: ReactNode }) => children,
}));
jest.mock('../services/api/RuntimeConfigApi', () => ({
  runtimeConfigApi: {
    getRuntimeConfiguration: jest.fn().mockResolvedValue({
      statusCode: 200,
      message: 'Runtime configuration loaded',
      data: {
        features: {
          emailDeliveryEnabled: false,
          emailTwoFactorEnabled: false,
          emailTwoFactorEnabledForNewUsers: false,
        },
      },
      errors: null,
      timestamp: '2026-07-21T00:00:00Z',
    }),
  },
}));

const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;

test('renders the public home experience for anonymous users', () => {
  mockedUseAuth.mockReturnValue({
    isAuthenticated: false,
    loading: false,
    user: null,
    error: null,
    tokens: null,
    login: jest.fn(),
    register: jest.fn(),
    logout: jest.fn(),
    refreshToken: jest.fn(),
    verifyTwoFactor: jest.fn(),
    resendTwoFactor: jest.fn(),
    updateDisplayName: jest.fn(),
    updateProfile: jest.fn(),
    changePassword: jest.fn(),
    clearError: jest.fn(),
  });

  render(<App />);

  expect(screen.getByRole('heading', { name: /professional auth flow, clear boundaries, zero guessing/i })).toBeInTheDocument();
  expect(screen.getByRole('link', { name: /login/i })).toBeInTheDocument();
  expect(screen.getByRole('link', { name: /register/i })).toBeInTheDocument();
  expect(screen.getByText(/nie jesteś zalogowany/i)).toBeInTheDocument();
});
