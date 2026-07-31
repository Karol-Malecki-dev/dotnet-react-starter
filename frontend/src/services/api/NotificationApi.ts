import type {
  GetNotificationsResponse,
  GetUnreadCountResponse,
  MarkAllNotificationsReadResponse,
  MarkNotificationReadResponse,
} from '../../types';
import { httpClient, type HttpClient } from './HttpClient';

export interface NotificationQuery {
  pageNumber?: number;
  pageSize?: number;
  unreadOnly?: boolean;
}

export class NotificationApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  getNotifications(query: NotificationQuery = {}): Promise<GetNotificationsResponse> {
    const params = new URLSearchParams();
    if (query.pageNumber !== undefined) params.set('pageNumber', String(query.pageNumber));
    if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize));
    if (query.unreadOnly !== undefined) params.set('unreadOnly', String(query.unreadOnly));
    const suffix = params.toString() ? `?${params.toString()}` : '';
    return this.client.get<GetNotificationsResponse>(`/notifications${suffix}`);
  }

  getUnreadCount(): Promise<GetUnreadCountResponse> {
    return this.client.get<GetUnreadCountResponse>('/notifications/unread-count');
  }

  markAsRead(notificationId: string): Promise<MarkNotificationReadResponse> {
    return this.client.patch<MarkNotificationReadResponse>(`/notifications/${notificationId}/read`);
  }

  markAllAsRead(): Promise<MarkAllNotificationsReadResponse> {
    return this.client.patch<MarkAllNotificationsReadResponse>('/notifications/read-all');
  }
}

export const notificationApi = new NotificationApi();
