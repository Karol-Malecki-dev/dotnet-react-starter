import { Bell } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useNotifications } from '../../hooks/useNotifications';

export function NotificationBell() {
  const navigate = useNavigate();
  const { unreadCount } = useNotifications();
  const label = unreadCount > 0
    ? `Notifications, ${unreadCount} unread`
    : 'Notifications';

  return (
    <button
      aria-label={label}
      className="icon-button notification-bell"
      title={label}
      type="button"
      onClick={() => navigate('/notifications')}
    >
      <Bell aria-hidden="true" size={19} strokeWidth={2} />
      {unreadCount > 0 ? <span className="notification-bell__count">{unreadCount > 99 ? '99+' : unreadCount}</span> : null}
    </button>
  );
}
