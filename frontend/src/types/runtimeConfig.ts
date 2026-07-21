/**
 * RUNTIME CONFIG TYPES - frontend bootstrap configuration returned by the backend.
 *
 * These map to backend Shared.Dtos.AppRuntimeConfigurationDto and AppFeatureFlagsDto.
 */

/** Feature flags safe to expose to the UI. */
export interface AppFeatureFlagsDto {
  emailDeliveryEnabled: boolean;
  emailTwoFactorEnabled: boolean;
  emailTwoFactorEnabledForNewUsers: boolean;
}

/** Backend runtime configuration exposed to the frontend bootstrap process. */
export interface AppRuntimeConfigurationDto {
  features: AppFeatureFlagsDto;
}