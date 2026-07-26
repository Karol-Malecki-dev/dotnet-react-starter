import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { Navbar } from '../../../components/UI/Navbar';
import { useAuth } from '../../../hooks/useAuth';
import { useFeatureAvailability } from '../../../hooks/useFeatureAvailability';

import { vi } from 'vitest';

vi.mock('../../../hooks/useAuth');
vi.mock('../../../hooks/useFeatureAvailability');
const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedUseFeatureAvailability = useFeatureAvailability as jest.MockedFunction<typeof useFeatureAvailability>;

const mockNavigate = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe('Navbar', () => {
  beforeEach(() => {
    mockNavigate.mockClear();
    jest.resetAllMocks();
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
      emailTwoFactorEnabled: true,
      emailTwoFactorEnabledForNewUsers: true,
      projectsEnabled: true,
      projectArchiveEnabled: true,
      projectTaskAssignmentEnabled: true,
    });
  });

  it('renders login and register links when user is not authenticated', () => {
    mockedUseAuth.mockReturnValue({
      isAuthenticated: false,
      user: null,
      logout: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    );

    expect(screen.getByRole('searchbox', { name: /project search/i })).toBeInTheDocument();
    expect(screen.getByText(/login/i)).toBeInTheDocument();
    expect(screen.getByText(/register/i)).toBeInTheDocument();
  });

  it('shows the logged in user and calls logout when button is clicked', async () => {
    const logout = jest.fn().mockResolvedValue(undefined);

    mockedUseAuth.mockReturnValue({
      isAuthenticated: true,
      user: { displayName: 'Test User', role: 'User' },
      logout,
    } as any);

    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    );

    expect(screen.getByText(/test user/i)).toBeInTheDocument();
    const logoutButton = screen.getByRole('button', { name: /logout/i });

    fireEvent.click(logoutButton);

    await waitFor(() => expect(logout).toHaveBeenCalledTimes(1));
    expect(mockNavigate).toHaveBeenCalledWith('/');
  });

  it('hides admin navigation for regular users', () => {
    mockedUseAuth.mockReturnValue({
      isAuthenticated: true,
      user: { displayName: 'Regular User', role: 'User' },
      logout: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    );

    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /profile/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /admin/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /users/i })).not.toBeInTheDocument();
  });

  it('shows admin navigation for admin users', () => {
    mockedUseAuth.mockReturnValue({
      isAuthenticated: true,
      user: { displayName: 'Admin User', role: 'Admin' },
      logout: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    );

    expect(screen.getByRole('link', { name: /admin/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /users/i })).toBeInTheDocument();
    expect(screen.getByText('Admin User')).toBeInTheDocument();
  });

  it('shows quick search suggestions when enabled', () => {
    mockedUseAuth.mockReturnValue({
      isAuthenticated: false,
      user: null,
      logout: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Navbar />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByRole('searchbox', { name: /project search/i }), { target: { value: 'register' } });

    expect(screen.getByRole('button', { name: /register/i })).toBeInTheDocument();
  });
});
