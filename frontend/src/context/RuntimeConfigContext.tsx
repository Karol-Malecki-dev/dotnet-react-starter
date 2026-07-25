import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { runtimeConfigApi } from '../services/api/RuntimeConfigApi';
import type { AppFeatureFlagsDto, AppRuntimeConfigurationDto } from '../types/runtimeConfig';

const defaultRuntimeConfiguration: AppRuntimeConfigurationDto = {
  features: {
    projectsEnabled: false,
    projectArchiveEnabled: false,
    projectTaskAssignmentEnabled: false,
    emailDeliveryEnabled: false,
    globalSearchEnabled: false,
    dashboardOverviewEnabled: false,
    adminNavigationEnabled: false,
    userManagementNavigationEnabled: false,
    emailFeatureSectionsEnabled: false,
    emailTwoFactorEnabled: false,
    emailTwoFactorEnabledForNewUsers: false,
  },
};

export interface RuntimeConfigState {
  runtimeConfiguration: AppRuntimeConfigurationDto;
  loading: boolean;
  loaded: boolean;
  error: string | null;
}

export interface RuntimeConfigContextType extends RuntimeConfigState {
  refresh: () => Promise<void>;
  isFeatureEnabled: (featureName: keyof AppFeatureFlagsDto) => boolean;
}

const RuntimeConfigContext = createContext<RuntimeConfigContextType | undefined>(undefined);

function normalizeRuntimeConfiguration(
  runtimeConfiguration: Partial<AppRuntimeConfigurationDto> | null | undefined,
): AppRuntimeConfigurationDto {
  const features: Partial<AppFeatureFlagsDto> = runtimeConfiguration?.features ?? {};

  return {
    features: {
      projectsEnabled: Boolean(features.projectsEnabled),
      projectArchiveEnabled: Boolean(features.projectArchiveEnabled),
      projectTaskAssignmentEnabled: Boolean(features.projectTaskAssignmentEnabled),
      emailDeliveryEnabled: Boolean(features.emailDeliveryEnabled),
      globalSearchEnabled: Boolean(features.globalSearchEnabled),
      dashboardOverviewEnabled: Boolean(features.dashboardOverviewEnabled),
      adminNavigationEnabled: Boolean(features.adminNavigationEnabled),
      userManagementNavigationEnabled: Boolean(features.userManagementNavigationEnabled),
      emailFeatureSectionsEnabled: Boolean(features.emailFeatureSectionsEnabled),
      emailTwoFactorEnabled: Boolean(features.emailTwoFactorEnabled),
      emailTwoFactorEnabledForNewUsers: Boolean(features.emailTwoFactorEnabledForNewUsers),
    },
  };
}

function resolveRuntimeConfigurationError(error: unknown): string {
  return error instanceof Error ? error.message : 'Unable to load runtime configuration';
}

export function RuntimeConfigProvider({ children }: { children: React.ReactNode }) {
  const isMountedRef = useRef(false);
  const [state, setState] = useState<RuntimeConfigState>({
    runtimeConfiguration: defaultRuntimeConfiguration,
    loading: true,
    loaded: false,
    error: null,
  });

  useEffect(() => {
    isMountedRef.current = true;

    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const refresh = useCallback(async () => {
    if (isMountedRef.current) {
      setState((current) => ({
        ...current,
        loading: true,
        error: null,
      }));
    }

    try {
      const response = await runtimeConfigApi.getRuntimeConfiguration();
      if (!response.data) {
        throw new Error('Runtime configuration payload is missing');
      }

      if (isMountedRef.current) {
        setState({
          runtimeConfiguration: normalizeRuntimeConfiguration(response.data),
          loading: false,
          loaded: true,
          error: null,
        });
      }
    } catch (error) {
      if (!isMountedRef.current) {
        return;
      }

      setState({
        runtimeConfiguration: defaultRuntimeConfiguration,
        loading: false,
        loaded: true,
        error: resolveRuntimeConfigurationError(error),
      });
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const value: RuntimeConfigContextType = {
    ...state,
    refresh,
    isFeatureEnabled: (featureName) => Boolean(state.runtimeConfiguration.features[featureName]),
  };

  return <RuntimeConfigContext.Provider value={value}>{children}</RuntimeConfigContext.Provider>;
}

export function useRuntimeConfigContext() {
  const context = useContext(RuntimeConfigContext);

  if (!context) {
    throw new Error('useRuntimeConfigContext must be used within RuntimeConfigProvider');
  }

  return context;
}