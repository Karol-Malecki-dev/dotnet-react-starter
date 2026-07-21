import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import UserList from '../../../pages/users/UserList';
import { useAuth } from '../../../hooks/useAuth';
import { adminApi } from '../../../services/api';
import { AdminUserRole } from '../../../types';
import type { AdminUserDetailsDto, AdminUserListItemDto } from '../../../types';

jest.mock('../../../hooks/useAuth');
jest.mock('../../../services/api', () => {
  const actual = jest.requireActual('../../../services/api');

  return {
    ...actual,
    adminApi: {
      getUsers: jest.fn(),
      getUserDetailsById: jest.fn(),
      deleteUser: jest.fn(),
      updateUserRole: jest.fn(),
      activateUser: jest.fn(),
      deactivateUser: jest.fn(),
    },
  };
});

const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;
const mockedAdminApi = adminApi as jest.Mocked<typeof adminApi>;

const createUser = (id: string, displayName: string, role: 'User' | 'Admin' = 'User'): AdminUserListItemDto => ({
  id,
  email: `${displayName.toLowerCase().replace(/\s+/g, '.')}@example.com`,
  displayName,
  role,
  isActive: true,
  isEmailConfirmed: true,
  createdAt: '2026-06-26T00:00:00Z',
});

const createDetails = (id: string, displayName: string, role: AdminUserRole): AdminUserDetailsDto => ({
  id,
  email: `${displayName.toLowerCase().replace(/\s+/g, '.')}@example.com`,
  displayName,
  avatarUrl: null,
  role,
  isActive: true,
  isEmailConfirmed: true,
  isTwoFactorEnabled: false,
  address: null,
  createdAt: '2026-06-26T00:00:00Z',
});

describe('UserList page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
    mockedUseAuth.mockReturnValue({
      user: { id: 'admin-user', displayName: 'Admin User', email: 'admin@example.com', role: 'Admin' },
    } as any);
  });

  it('loads users with default pagination and renders the directory', async () => {
    mockedAdminApi.getUsers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [createUser('user-1', 'Alice Tester')],
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <MemoryRouter>
        <UserList />
      </MemoryRouter>,
    );

    expect(await screen.findByText(/alice tester/i)).toBeInTheDocument();
    expect(mockedAdminApi.getUsers).toHaveBeenCalledWith(expect.objectContaining({ pageNumber: 1, pageSize: 10 }));
    expect(screen.getByText(/admin access/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /delete/i })).toBeInTheDocument();
  });

  it('allows admins to request role changes', async () => {
    mockedAdminApi.getUsers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [createUser('user-1', 'Alice Tester', 'User')],
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });
    mockedAdminApi.updateUserRole.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: createDetails('user-1', 'Alice Tester', AdminUserRole.Admin),
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <MemoryRouter>
        <UserList />
      </MemoryRouter>,
    );

    await screen.findByText(/alice tester/i);
    fireEvent.click(screen.getByRole('button', { name: /set admin/i }));

    await waitFor(() => expect(mockedAdminApi.updateUserRole).toHaveBeenCalledWith('user-1', AdminUserRole.Admin));
    expect(await screen.findByText(/role updated to admin/i)).toBeInTheDocument();
  });

  it('loads user details when details are requested', async () => {
    mockedAdminApi.getUsers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: [createUser('user-1', 'Alice Tester', 'User')],
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });
    mockedAdminApi.getUserDetailsById.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: createDetails('user-1', 'Alice Tester', AdminUserRole.User),
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <MemoryRouter>
        <UserList />
      </MemoryRouter>,
    );

    await screen.findByText(/alice tester/i);
    fireEvent.click(screen.getByRole('button', { name: /view details/i }));

    await waitFor(() => expect(mockedAdminApi.getUserDetailsById).toHaveBeenCalledWith('user-1'));
    const detailsPanel = screen.getByRole('complementary', { name: /selected user/i });
    expect(await within(detailsPanel).findByText(/alice\.tester@example\.com/i)).toBeInTheDocument();
  });

  it('requests the next page when pagination advances', async () => {
    const firstPageUsers = Array.from({ length: 10 }, (_, index) => createUser(`user-${index}`, `User ${index}`));
    mockedAdminApi.getUsers.mockResolvedValue({
      statusCode: 200,
      message: 'OK',
      data: firstPageUsers,
      errors: null,
      timestamp: '2026-06-26T00:00:00Z',
    });

    render(
      <MemoryRouter>
        <UserList />
      </MemoryRouter>,
    );

    await screen.findByText(/user 0/i);
    fireEvent.click(screen.getByRole('button', { name: /next/i }));

    await waitFor(() => expect(mockedAdminApi.getUsers).toHaveBeenLastCalledWith(expect.objectContaining({ pageNumber: 2, pageSize: 10 })));
  });
});