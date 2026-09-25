import { useEffect, useState } from 'react';

function getInitialOnlineState() {
  return typeof navigator === 'undefined' || navigator.onLine;
}

export function NetworkStatusBanner() {
  const [isOnline, setIsOnline] = useState(getInitialOnlineState);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  if (isOnline) {
    return null;
  }

  return (
    <div className="global-notice global-notice--warning" role="status" aria-live="polite">
      <span>You are offline. New data and changes may not load until the connection is restored.</span>
    </div>
  );
}
