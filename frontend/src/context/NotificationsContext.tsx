import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react';
import { notificationApi } from '../services/api';
import type { NotificationDto } from '../types';
import { useAuth } from '../hooks/useAuth';

interface NotificationsContextValue {
  notifications: NotificationDto[];
  unreadCount: number;
  loading: boolean;
  error: string | null;
  refreshNotifications: (unreadOnly?: boolean) => Promise<void>;
  markAsRead: (notificationId: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
}

const NotificationsContext = createContext<NotificationsContextValue | undefined>(undefined);

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refreshNotifications = useCallback(async (unreadOnly = false) => {
    if (!isAuthenticated) {
      setNotifications([]);
      setUnreadCount(0);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const response = await notificationApi.getNotifications({ pageSize: 50, unreadOnly });
      if (!response.data) {
        throw new Error('Notification response is missing data.');
      }

      setNotifications(response.data.items);
      setUnreadCount(response.data.unreadCount);
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Unable to load notifications.');
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    void refreshNotifications();
  }, [refreshNotifications]);

  const markAsRead = useCallback(async (notificationId: string) => {
    const response = await notificationApi.markAsRead(notificationId);
    if (!response.data) {
      throw new Error(response.message || 'Notification was not updated.');
    }

    const updatedNotification = response.data;
    const wasUnread = notifications.some((notification) => notification.id === notificationId && !notification.isRead);

    setNotifications((current) => current.map((notification) => (
      notification.id === notificationId ? updatedNotification : notification
    )));
    if (wasUnread) {
      setUnreadCount((current) => Math.max(0, current - 1));
    }
  }, [notifications]);

  const markAllAsRead = useCallback(async () => {
    await notificationApi.markAllAsRead();
    const readAt = new Date().toISOString();
    setNotifications((current) => current.map((notification) => (
      notification.isRead ? notification : { ...notification, isRead: true, readAt }
    )));
    setUnreadCount(0);
  }, []);

  return (
    <NotificationsContext.Provider value={{
      notifications,
      unreadCount,
      loading,
      error,
      refreshNotifications,
      markAsRead,
      markAllAsRead,
    }}>
      {children}
    </NotificationsContext.Provider>
  );
}

export function useNotificationsContext() {
  const context = useContext(NotificationsContext);
  if (!context) {
    throw new Error('useNotificationsContext must be used within NotificationsProvider');
  }

  return context;
}
