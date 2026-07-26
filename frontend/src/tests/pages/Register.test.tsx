import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import Register from '../../pages/Register';
import { useAuth } from '../../hooks/useAuth';
import { HttpError } from '../../services/api';

import { vi } from 'vitest';

vi.mock('../../hooks/useAuth');
const mockedUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;

describe('Register page', () => {
  beforeEach(() => {
    jest.resetAllMocks();
  });

  it('submits all fields required by the backend register contract', async () => {
    const register = jest.fn().mockResolvedValue({
      email: 'karol@example.com',
      requiresEmailConfirmation: true,
    });

    mockedUseAuth.mockReturnValue({
      register,
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Karol' } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Kowalski' } });
    fireEvent.change(screen.getByLabelText(/^email$/i), { target: { value: 'karol@example.com' } });
    fireEvent.change(screen.getByLabelText(/phone/i), { target: { value: '+48 123 456 789' } });
    fireEvent.change(screen.getByLabelText(/address/i), { target: { value: 'Warsaw' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(register).toHaveBeenCalledWith({
        firstName: 'Karol',
        lastName: 'Kowalski',
        email: 'karol@example.com',
        password: 'password123',
        phoneNumber: '+48 123 456 789',
        address: 'Warsaw',
      });
    });
  });

  it('shows a friendly rate-limit message when registration receives 429', async () => {
    const register = jest.fn().mockRejectedValue(new HttpError(429, 'Too many requests', null));

    mockedUseAuth.mockReturnValue({
      register,
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Karol' } });
    fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Kowalski' } });
    fireEvent.change(screen.getByLabelText(/^email$/i), { target: { value: 'karol@example.com' } });
    fireEvent.change(screen.getByLabelText(/phone/i), { target: { value: '+48 123 456 789' } });
    fireEvent.change(screen.getByLabelText(/address/i), { target: { value: 'Warsaw' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText(/registration is temporarily rate limited/i)).toBeInTheDocument();
  });

  it('shows validation errors when register fields are malformed', async () => {
    mockedUseAuth.mockReturnValue({
      register: jest.fn(),
      loading: false,
      error: null,
      clearError: jest.fn(),
    } as any);

    render(
      <MemoryRouter>
        <Register />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByLabelText(/^email$/i), { target: { value: 'wrong-format' } });
    fireEvent.change(screen.getByLabelText(/phone/i), { target: { value: '123' } });
    fireEvent.change(screen.getByLabelText(/address/i), { target: { value: 'ab' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: '123' } });
    fireEvent.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText(/first name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/last name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/enter a valid email address/i)).toBeInTheDocument();
    expect(screen.getByText(/phone number must be at least 6 characters long/i)).toBeInTheDocument();
    expect(screen.getByText(/address must be at least 3 characters long/i)).toBeInTheDocument();
    expect(screen.getByText(/password must be at least 8 characters long/i)).toBeInTheDocument();
  });
});