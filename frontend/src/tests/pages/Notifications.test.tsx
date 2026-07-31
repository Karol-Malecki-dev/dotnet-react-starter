import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import Notifications from '../../pages/Notifications';
import { useNotifications } from '../../hooks/useNotifications';
import { NotificationType } from '../../types';

vi.mock('../../hooks/useNotifications');

const mockNavigate = vi.hoisted(() => vi.fn());
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => mockNavigate };
});

const mockedUseNotifications = useNotifications as jest.MockedFunction<typeof useNotifications>;

describe('Notifications page', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
  });

  it('opens the related task and marks an unread task notification as read', async () => {
    const markAsRead = jest.fn().mockResolvedValue(undefined);
    mockedUseNotifications.mockReturnValue({
      notifications: [{
        id: 'notification-1',
        type: NotificationType.TaskDeadlineApproaching,
        title: 'Task deadline approaching',
        message: 'Review the release notes.',
        resourceType: 'ProjectTask',
        resourceId: 'task-1',
        projectId: 'project-1',
        createdAt: '2026-07-31T10:00:00Z',
        readAt: null,
        isRead: false,
      }],
      unreadCount: 1,
      loading: false,
      error: null,
      refreshNotifications: jest.fn(),
      markAsRead,
      markAllAsRead: jest.fn(),
    });

    render(<MemoryRouter><Notifications /></MemoryRouter>);

    fireEvent.click(screen.getByRole('button', { name: 'Open task' }));

    await waitFor(() => expect(markAsRead).toHaveBeenCalledWith('notification-1'));
    expect(mockNavigate).toHaveBeenCalledWith('/projects?projectId=project-1&taskId=task-1');
  });
});