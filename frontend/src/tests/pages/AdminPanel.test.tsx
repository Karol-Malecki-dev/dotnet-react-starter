import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import AdminPanel from '../../pages/AdminPanel';
import { adminApi } from '../../services/api';

vi.mock('../../services/api', async () => {
  const actual = await vi.importActual<typeof import('../../services/api')>('../../services/api');

  return {
    ...actual,
    adminApi: {
      getDashboardStats: jest.fn(),
    },
  };
});

const mockedAdminApi = adminApi as jest.Mocked<typeof adminApi>;

describe('AdminPanel page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('renders the backend dashboard stats', async () => {
    mockedAdminApi.getDashboardStats.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: {
        totalUsers: 42,
        activeUsers: 40,
        inactiveUsers: 2,
        newUsersLast7Days: 5,
        adminUsers: 3,
        activeAdminUsers: 3,
      },
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <MemoryRouter>
        <AdminPanel />
      </MemoryRouter>
    );

    expect(await screen.findByRole('heading', { name: '42' })).toBeInTheDocument();
    expect(screen.getByText(/active admins/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /open user directory/i })).toBeInTheDocument();
  });

  it('renders an error state when the admin overview fails', async () => {
    mockedAdminApi.getDashboardStats.mockRejectedValue(new Error('Access denied'));

    render(
      <MemoryRouter>
        <AdminPanel />
      </MemoryRouter>
    );

    expect(await screen.findByText(/access denied/i)).toBeInTheDocument();
  });

  it('renders a 403 API message when the admin overview is forbidden', async () => {
    mockedAdminApi.getDashboardStats.mockRejectedValue(new Error('Forbidden'));

    render(
      <MemoryRouter>
        <AdminPanel />
      </MemoryRouter>
    );

    expect(await screen.findByText(/forbidden/i)).toBeInTheDocument();
  });

  it('falls back to a generic message for non-Error failures', async () => {
    mockedAdminApi.getDashboardStats.mockRejectedValue('unexpected failure');

    render(
      <MemoryRouter>
        <AdminPanel />
      </MemoryRouter>
    );

    expect(await screen.findByText(/failed to load admin overview/i)).toBeInTheDocument();
  });
});