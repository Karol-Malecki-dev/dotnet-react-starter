import { AppNoticeCenter } from './UI/AppNoticeCenter';
import { Navbar } from './UI/Navbar';
import { AppRoutes } from './AppRoutes';

export function AppShell() {
  return (
    <div className="app-shell">
      <AppNoticeCenter />
      <Navbar />
      <main className="app-shell__main">
        <AppRoutes />
      </main>
    </div>
  );
}