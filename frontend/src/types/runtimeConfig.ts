/**
 * RUNTIME CONFIG TYPES - frontend bootstrap configuration returned by the backend.
 *
 * These map to backend Shared.Dtos.AppRuntimeConfigurationDto and AppFeatureFlagsDto.
 */

/** Feature flags safe to expose to the UI. */
export interface AppFeatureFlagsDto {
  projectsEnabled: boolean;
  projectArchiveEnabled: boolean;
  projectTaskAssignmentEnabled: boolean;
  emailDeliveryEnabled: boolean;
  globalSearchEnabled: boolean;
  dashboardOverviewEnabled: boolean;
  adminNavigationEnabled: boolean;
  userManagementNavigationEnabled: boolean;
  emailFeatureSectionsEnabled: boolean;
  emailTwoFactorEnabled: boolean;
  emailTwoFactorEnabledForNewUsers: boolean;
}

/** Backend runtime configuration exposed to the frontend bootstrap process. */
export interface AppRuntimeConfigurationDto {
  features: AppFeatureFlagsDto;
}