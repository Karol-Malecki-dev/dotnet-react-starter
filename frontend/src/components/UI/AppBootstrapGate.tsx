import { useAuth } from '../../hooks/useAuth';
import { useFeatureAvailability } from '../../hooks/useFeatureAvailability';
import type { ReactNode } from 'react';

interface AppBootstrapGateProps {
  children: ReactNode;
}

export function AppBootstrapGate({ children }: AppBootstrapGateProps) {
  const { loading: authLoading } = useAuth();
  const { loading: runtimeLoading } = useFeatureAvailability();

  if (authLoading || runtimeLoading) {
    return (
      <div className="app-shell__bootstrap">
        <div className="page-state page-state--loading">
          <span className="page-state__spinner" aria-hidden="true" />
          <div>
            <h1>Loading application shell</h1>
            <p>Fetching session state and runtime feature flags...</p>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}