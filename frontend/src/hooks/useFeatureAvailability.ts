import { useRuntimeConfig } from './useRuntimeConfig';

export interface FeatureAvailability {
  loading: boolean;
  loaded: boolean;
  error: string | null;
  globalSearchEnabled: boolean;
  dashboardOverviewEnabled: boolean;
  adminNavigationEnabled: boolean;
  userManagementNavigationEnabled: boolean;
  emailFeatureSectionsEnabled: boolean;
  emailDeliveryEnabled: boolean;
  emailTwoFactorEnabled: boolean;
  emailTwoFactorEnabledForNewUsers: boolean;
}

export function useFeatureAvailability(): FeatureAvailability {
  const { loading, loaded, error, isFeatureEnabled } = useRuntimeConfig();

  return {
    loading,
    loaded,
    error,
    globalSearchEnabled: isFeatureEnabled('globalSearchEnabled'),
    dashboardOverviewEnabled: isFeatureEnabled('dashboardOverviewEnabled'),
    adminNavigationEnabled: isFeatureEnabled('adminNavigationEnabled'),
    userManagementNavigationEnabled: isFeatureEnabled('userManagementNavigationEnabled'),
    emailFeatureSectionsEnabled: isFeatureEnabled('emailFeatureSectionsEnabled'),
    emailDeliveryEnabled: isFeatureEnabled('emailDeliveryEnabled'),
    emailTwoFactorEnabled: isFeatureEnabled('emailTwoFactorEnabled'),
    emailTwoFactorEnabledForNewUsers: isFeatureEnabled('emailTwoFactorEnabledForNewUsers'),
  };
}