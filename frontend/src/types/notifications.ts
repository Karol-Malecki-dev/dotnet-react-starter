import type { ApiResponse } from './api';

export enum NotificationType {
  ProjectInvitation = 1,
  TaskAssigned = 2,
  SecurityAlert = 3,
  System = 4,
}

export interface NotificationDto {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  resourceType: string | null;
  resourceId: string | null;
  createdAt: string;
  readAt: string | null;
  isRead: boolean;
}

export interface NotificationPageDto {
  items: NotificationDto[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  unreadCount: number;
}

export type GetNotificationsResponse = ApiResponse<NotificationPageDto>;
export type GetUnreadCountResponse = ApiResponse<number>;
export type MarkNotificationReadResponse = ApiResponse<NotificationDto>;
export type MarkAllNotificationsReadResponse = ApiResponse<number>;
