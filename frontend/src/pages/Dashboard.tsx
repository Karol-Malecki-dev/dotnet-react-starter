import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';

export default function Dashboard() {
  const { user, tokens, isAuthenticated } = useAuth();
  const {
    dashboardOverviewEnabled,
    globalSearchEnabled,
    emailFeatureSectionsEnabled,
    emailDeliveryEnabled,
    emailTwoFactorEnabled,
  } = useFeatureAvailability();
  const isAdmin = user?.role === 'Admin';

  return (
    <section className="page-shell">
      <h1>Dashboard</h1>
      <p>Protected workspace for authenticated users with feature-gated navigation and runtime shell data.</p>

      {dashboardOverviewEnabled ? (
        <>
          <div className="grid grid--2">
            <article className="card">
              <h2>Session</h2>
              <p>Status: {isAuthenticated ? 'authenticated' : 'anonymous'}</p>
              <p>Access token expires in: {tokens?.expiresIn ?? 0}s</p>
              <p>Quick search: {globalSearchEnabled ? 'enabled' : 'disabled'}</p>
            </article>

            <article className="card">
              <h2>User</h2>
              {user ? (
                <>
                  <p>{user.displayName}</p>
                  <p>{user.email}</p>
                  <p>{user.role}</p>
                </>
              ) : (
                <p>Brak załadowanego usera.</p>
              )}
            </article>
          </div>

          {emailFeatureSectionsEnabled ? (
            <article className="card">
              <h2>Runtime feature snapshot</h2>
              <div className="grid grid--cards">
                <p>Email delivery: {emailDeliveryEnabled ? 'enabled' : 'disabled'}</p>
                <p>Two-factor auth: {emailTwoFactorEnabled ? 'enabled' : 'disabled'}</p>
                <p>Admin controls: {isAdmin ? 'available' : 'hidden by role'}</p>
              </div>
            </article>
          ) : null}

          <div className="hero__actions">
            <Link className="button" to="/profile">
              Profile
            </Link>
            {isAdmin ? (
              <>
                <Link className="button button--ghost" to="/admin">
                  Admin panel
                </Link>
                <Link className="button button--ghost" to="/admin/users">
                  Users directory
                </Link>
              </>
            ) : null}
          </div>
        </>
      ) : (
        <div className="page-state">
          <h2>Dashboard overview is disabled</h2>
          <p>This environment hides the dashboard shell via runtime config.</p>
        </div>
      )}
    </section>
  );
}
