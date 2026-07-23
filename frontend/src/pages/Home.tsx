import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';

export default function Home() {
  const { isAuthenticated, user } = useAuth();
  const {
    emailFeatureSectionsEnabled,
    emailDeliveryEnabled,
    emailTwoFactorEnabled,
    emailTwoFactorEnabledForNewUsers,
    globalSearchEnabled,
    dashboardOverviewEnabled,
  } = useFeatureAvailability();

  return (
    <section className="hero">
      <div className="hero__copy">
        <p className="eyebrow">Frontend JWT architecture</p>
        <h1>Professional auth flow, clear boundaries, zero guessing.</h1>
        <p>
          Ten projekt prowadzi Cię przez prawidłowy podział na typy, serwisy API, context i chronione strony.
        </p>
        <div className="hero__actions">
          <Link className="button" to="/dashboard">
            Open dashboard
          </Link>
          <Link className="button button--ghost" to="/users">
            Manage users
          </Link>
        </div>
      </div>

      <div className="hero__stack">
        <aside className="hero__panel">
          <h2>Current session</h2>
          {isAuthenticated && user ? (
            <>
              <p>{user.displayName}</p>
              <p>{user.email}</p>
              <p>{user.role}</p>
            </>
          ) : (
            <p>Nie jesteś zalogowany. Wejdź do login/register lub podepnij backend JWT.</p>
          )}
        </aside>

        {emailFeatureSectionsEnabled ? (
          <aside className="hero__panel">
            <h2>Runtime features</h2>
            <div className="stack stack--compact">
              <p>Quick search: {globalSearchEnabled ? 'enabled' : 'disabled'}</p>
              <p>Dashboard overview: {dashboardOverviewEnabled ? 'enabled' : 'disabled'}</p>
              <p>Email delivery: {emailDeliveryEnabled ? 'enabled' : 'disabled'}</p>
              <p>Two-factor auth: {emailTwoFactorEnabled ? 'enabled' : 'disabled'}</p>
              <p>2FA for new users: {emailTwoFactorEnabledForNewUsers ? 'enabled' : 'disabled'}</p>
            </div>
          </aside>
        ) : null}
      </div>
    </section>
  );
}
