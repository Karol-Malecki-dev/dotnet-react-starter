import { useNotificationsContext } from '../context/NotificationsContext';

export function useNotifications() {
  return useNotificationsContext();
}
