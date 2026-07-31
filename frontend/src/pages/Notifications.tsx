import { CheckCheck, Check, Inbox, ArrowRight } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useNotifications } from '../hooks/useNotifications';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export default function Notifications() {
  const { notifications, unreadCount, loading, error, refreshNotifications, markAsRead, markAllAsRead } = useNotifications();
  const navigate = useNavigate();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [updating, setUpdating] = useState(false);

  const handleFilterChange = async (nextUnreadOnly: boolean) => {
    setUnreadOnly(nextUnreadOnly);
    await refreshNotifications(nextUnreadOnly);
  };

  const handleMarkAllAsRead = async () => {
    setUpdating(true);
    try {
      await markAllAsRead();
    } finally {
      setUpdating(false);
    }
  };

  const handleMarkAsRead = async (notificationId: string) => {
    setUpdating(true);
    try {
      await markAsRead(notificationId);
    } finally {
      setUpdating(false);
    }
  };

  const openTask = async (notification: typeof notifications[number]) => {
    if (notification.resourceType !== 'ProjectTask' || !notification.resourceId || !notification.projectId) return;

    if (!notification.isRead) {
      await handleMarkAsRead(notification.id);
    }

    navigate(`/projects?projectId=${encodeURIComponent(notification.projectId)}&taskId=${encodeURIComponent(notification.resourceId)}`);
  };

  return (
    <section className="page-shell">
      <div className="page-shell__header">
        <div className="stack stack--tight">
          <p className="eyebrow">Workspace</p>
          <h1>Notifications</h1>
          <p className="page-note">{unreadCount} unread notification{unreadCount === 1 ? '' : 's'}.</p>
        </div>
        <button
          className="button button--ghost"
          disabled={updating || unreadCount === 0}
          type="button"
          onClick={() => void handleMarkAllAsRead()}
        >
          <CheckCheck aria-hidden="true" size={18} />
          Mark all read
        </button>
      </div>

      <div className="notification-toolbar" role="group" aria-label="Notification filter">
        <button
          className={`segmented-control__button ${!unreadOnly ? 'segmented-control__button--active' : ''}`}
          type="button"
          onClick={() => void handleFilterChange(false)}
        >
          All
        </button>
        <button
          className={`segmented-control__button ${unreadOnly ? 'segmented-control__button--active' : ''}`}
          type="button"
          onClick={() => void handleFilterChange(true)}
        >
          Unread
        </button>
      </div>

      {loading ? <p role="status">Loading notifications...</p> : null}
      {error ? <p className="form__error" role="alert">{error}</p> : null}
      {!loading && !error && notifications.length === 0 ? (
        <div className="page-state stack stack--tight">
          <Inbox aria-hidden="true" size={28} />
          <h2>No notifications here</h2>
        </div>
      ) : null}
      {!loading && notifications.length > 0 ? (
        <div className="notification-list">
          {notifications.map((notification) => (
            <article className={`notification-list__item ${notification.isRead ? '' : 'notification-list__item--unread'}`} key={notification.id}>
              <div className="stack stack--tight">
                <div className="notification-list__heading">
                  <h2>{notification.title}</h2>
                  <time dateTime={notification.createdAt}>{formatDate(notification.createdAt)}</time>
                </div>
                <p>{notification.message}</p>
              </div>
              {notification.resourceType === 'ProjectTask' && notification.resourceId && notification.projectId ? (
                <button className="button button--ghost" type="button" onClick={() => void openTask(notification)}>
                  Open task
                  <ArrowRight aria-hidden="true" size={16} />
                </button>
              ) : null}
              {!notification.isRead ? (
                <button
                  aria-label={`Mark ${notification.title} as read`}
                  className="icon-button"
                  disabled={updating}
                  title="Mark as read"
                  type="button"
                  onClick={() => void handleMarkAsRead(notification.id)}
                >
                  <Check aria-hidden="true" size={18} />
                </button>
              ) : null}
            </article>
          ))}
        </div>
      ) : null}
    </section>
  );
}
