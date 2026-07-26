import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import Login from '../../pages/Login';
import { useAuth } from '../../hooks/useAuth';
import { HttpError } from '../../services/api';

import { vi } from 'vitest';

vi.mock('../../hooks/useAuth');
const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;

describe('Login page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('submits the login form and calls login with entered credentials', async () => {
    const login = jest.fn().mockResolvedValue({ kind: 'authenticated' });
    const clearError = jest.fn();

    mockedUseAuth.mockReturnValue({
      login,
      loading: false,
      error: null,
      clearError,
    } as any);

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'test@example.com' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(login).toHaveBeenCalledWith('test@example.com', 'password123');
      expect(clearError).toHaveBeenCalled();
    });
  });

  it('renders an error message when login fails', async () => {
    const login = jest.fn().mockRejectedValue(new Error('Invalid credentials'));
    const clearError = jest.fn();

    mockedUseAuth.mockReturnValue({
      login,
      loading: false,
      error: 'Invalid credentials',
      clearError,
    } as any);

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>
    );

    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument();
    expect(screen.getByText(/^invalid credentials$/i)).toBeInTheDocument();
  });

  it('shows a friendly rate-limit message when login receives 429', async () => {
    const login = jest.fn().mockRejectedValue(new HttpError(429, 'Too many requests', null));

    mockedUseAuth.mockReturnValue({
      login,
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'test@example.com' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/too many login attempts/i)).toBeInTheDocument();
  });

  it('shows validation errors before submitting an invalid login form', async () => {
    mockedUseAuth.mockReturnValue({
      login: jest.fn(),
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Login />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'wrong-format' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: '123' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/enter a valid email address/i)).toBeInTheDocument();
    expect(screen.getByText(/password must be at least 8 characters long/i)).toBeInTheDocument();
  });

  it('shows a session-expired message when redirected back to login', async () => {
    mockedUseAuth.mockReturnValue({
      login: jest.fn(),
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter initialEntries={[{ pathname: '/login', state: { reason: 'session-expired' } } as any]}>
        <Login />
      </MemoryRouter>
    );

    expect(await screen.findByText(/your session expired. please sign in again/i)).toBeInTheDocument();
  });
});
