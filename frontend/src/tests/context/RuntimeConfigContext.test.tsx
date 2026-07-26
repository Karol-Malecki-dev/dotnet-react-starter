import { render, screen, waitFor } from '@testing-library/react';
import { useRuntimeConfig } from '../../hooks/useRuntimeConfig';
import { RuntimeConfigProvider } from '../../context/RuntimeConfigContext';
import { runtimeConfigApi } from '../../services/api/RuntimeConfigApi';

import { vi } from 'vitest';

vi.mock('../../services/api/RuntimeConfigApi', () => ({
  runtimeConfigApi: {
    getRuntimeConfiguration: jest.fn(),
  },
}));

const mockedRuntimeConfigApi = {
  runtimeConfigApi: {
    getRuntimeConfiguration: vi.mocked(runtimeConfigApi.getRuntimeConfiguration),
  },
};

function RuntimeConfigConsumer() {
  const { runtimeConfiguration, loading, loaded, error, isFeatureEnabled } = useRuntimeConfig();

  return (
    <>
      <div data-testid="status">{loading ? 'loading' : loaded ? 'loaded' : 'idle'}</div>
      <div data-testid="email-delivery">{String(runtimeConfiguration.features.emailDeliveryEnabled)}</div>
      <div data-testid="two-factor">{String(isFeatureEnabled('emailTwoFactorEnabled'))}</div>
      <div data-testid="error">{error ?? 'none'}</div>
    </>
  );
}

describe('RuntimeConfigContext', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('loads runtime configuration from the backend', async () => {
    mockedRuntimeConfigApi.runtimeConfigApi.getRuntimeConfiguration.mockResolvedValue({
      statusCode: 200,
      message: 'Runtime configuration loaded',
      data: {
        features: {
          projectsEnabled: false,
          projectArchiveEnabled: false,
          projectTaskAssignmentEnabled: false,
          emailDeliveryEnabled: true,
          globalSearchEnabled: true,
          dashboardOverviewEnabled: true,
          adminNavigationEnabled: true,
          userManagementNavigationEnabled: true,
          emailFeatureSectionsEnabled: true,
          emailTwoFactorEnabled: true,
          emailTwoFactorEnabledForNewUsers: false,
        },
      },
      errors: null,
      timestamp: '2026-07-21T00:00:00Z',
    });

    render(
      <RuntimeConfigProvider>
        <RuntimeConfigConsumer />
      </RuntimeConfigProvider>,
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('loaded'));
    expect(screen.getByTestId('email-delivery')).toHaveTextContent('true');
    expect(screen.getByTestId('two-factor')).toHaveTextContent('true');
    expect(screen.getByTestId('error')).toHaveTextContent('none');
  });

  it('falls back to safe defaults when the backend configuration cannot be loaded', async () => {
    mockedRuntimeConfigApi.runtimeConfigApi.getRuntimeConfiguration.mockRejectedValue(new Error('Network error'));

    render(
      <RuntimeConfigProvider>
        <RuntimeConfigConsumer />
      </RuntimeConfigProvider>,
    );

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('loaded'));
    expect(screen.getByTestId('email-delivery')).toHaveTextContent('false');
    expect(screen.getByTestId('two-factor')).toHaveTextContent('false');
    expect(screen.getByTestId('error')).toHaveTextContent('Network error');
  });
});