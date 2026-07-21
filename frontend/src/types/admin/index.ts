import type { ErrorDetail } from '../api';

/**
 * ADMIN TYPES - DTOs for admin dashboard and user-management endpoints.
 *
 * These map to backend Application.DTOs.Admin.* contracts.
 */

/** Mirrors backend Domain.Enums.UserRole for admin query/update operations. */
export enum AdminUserRole {
  User = 0,
  Admin = 1,
}

/** Dashboard metrics returned by the admin overview endpoint. */
export interface AdminDashboardStatsDto {
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  newUsersLast7Days: number;
  adminUsers: number;
  activeAdminUsers: number;
}

/** Compact admin list item used by the user directory page. */
export interface AdminUserListItemDto {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  isEmailConfirmed: boolean;
  createdAt: string;
}

/** Full admin view of a user record. */
export interface AdminUserDetailsDto {
  id: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  role: AdminUserRole;
  isActive: boolean;
  isEmailConfirmed: boolean;
  isTwoFactorEnabled: boolean;
  address?: string | null;
  createdAt: string;
}

/** Query payload used by the admin users listing endpoint. */
export interface AdminUserFilterRequestDto {
  ids?: string[];
  emails?: string[];
  roles?: AdminUserRole[];
  isActive?: boolean;
  isEmailConfirmed?: boolean;
  isTwoFactorEnabled?: boolean;
  pageNumber: number;
  pageSize: number;
}

/** Request body for the admin update-user endpoint. */
export interface AdminUpdateUserRequestDto {
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  isActive: boolean;
  isEmailConfirmed: boolean;
  isTwoFactorEnabled: boolean;
  address?: string | null;
}

export type AdminApiErrorDetail = ErrorDetail;