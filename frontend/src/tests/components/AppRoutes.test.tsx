import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../../components/AppRoutes';
import { useAuth } from '../../hooks/useAuth';
import { useFeatureAvailability } from '../../hooks/useFeatureAvailability';

jest.mock('../../hooks/useAuth');
jest.mock('../../hooks/useFeatureAvailability');

const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedUseFeatureAvailability = useFeatureAvailability as jest.MockedFunction<typeof useFeatureAvailability>;

describe('AppRoutes', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('redirects dashboard requests to the home page when the dashboard feature is disabled', () => {
    mockedUseAuth.mockReturnValue({ isAuthenticated: true, loading: false, user: { role: 'User' } } as any);
    mockedUseFeatureAvailability.mockReturnValue({
      loading: false,
      loaded: true,
      error: null,
      globalSearchEnabled: true,
      dashboardOverviewEnabled: false,
      adminNavigationEnabled: false,
      userManagementNavigationEnabled: false,
      emailFeatureSectionsEnabled: true,
      emailDeliveryEnabled: false,
      emailTwoFactorEnabled: false,
      emailTwoFactorEnabledForNewUsers: false,
    } as any);

    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AppRoutes />
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: /professional auth flow, clear boundaries, zero guessing/i })).toBeInTheDocument();
  });
});