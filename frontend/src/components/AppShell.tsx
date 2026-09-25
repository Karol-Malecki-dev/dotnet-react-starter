import { AppNoticeCenter } from './UI/AppNoticeCenter';
import { NetworkStatusBanner } from './UI/NetworkStatusBanner';
import { Navbar } from './UI/Navbar';
import { AppRoutes } from './AppRoutes';

export function AppShell() {
  return (
    <div className="app-shell">
      <NetworkStatusBanner />
      <AppNoticeCenter />
      <Navbar />
      <main className="app-shell__main">
        <AppRoutes />
      </main>
    </div>
  );
}