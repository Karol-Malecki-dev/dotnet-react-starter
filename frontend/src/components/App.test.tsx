import { render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import App from '../App';
import { useAuth } from '../hooks/useAuth';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';

jest.mock('../hooks/useAuth');
jest.mock('../hooks/useFeatureAvailability');
jest.mock('../context/RuntimeConfigContext', () => ({
  RuntimeConfigProvider: ({ children }: { children: ReactNode }) => children,
}));

const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedUseFeatureAvailability = useFeatureAvailability as jest.MockedFunction<typeof useFeatureAvailability>;

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
  mockedUseFeatureAvailability.mockReturnValue({
    loading: false,
    loaded: true,
    error: null,
    globalSearchEnabled: true,
    dashboardOverviewEnabled: true,
    adminNavigationEnabled: true,
    userManagementNavigationEnabled: true,
    emailFeatureSectionsEnabled: true,
    emailDeliveryEnabled: false,
    emailTwoFactorEnabled: false,
    emailTwoFactorEnabledForNewUsers: false,
  });

  render(<App />);

  expect(screen.getByRole('heading', { name: /professional auth flow, clear boundaries, zero guessing/i })).toBeInTheDocument();
  expect(screen.getByRole('searchbox', { name: /project search/i })).toBeInTheDocument();
  expect(screen.getByRole('link', { name: /login/i })).toBeInTheDocument();
  expect(screen.getByRole('link', { name: /register/i })).toBeInTheDocument();
  expect(screen.getByText(/nie jesteś zalogowany/i)).toBeInTheDocument();
});
