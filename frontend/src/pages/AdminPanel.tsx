import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { adminApi } from '../services/api';
import type { AdminDashboardStatsDto } from '../types';
import { getApiErrorMessage } from '../utils/helpers';

const emptyStats: AdminDashboardStatsDto = {
  totalUsers: 0,
  activeUsers: 0,
  inactiveUsers: 0,
  newUsersLast7Days: 0,
  adminUsers: 0,
  activeAdminUsers: 0,
};

export default function AdminPanel() {
  const [stats, setStats] = useState<AdminDashboardStatsDto>(emptyStats);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadStats = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await adminApi.getDashboardStats();
      setStats(response.data ?? emptyStats);
    } catch (caughtError) {
      setError(
        getApiErrorMessage(caughtError, {
          defaultMessage: 'Failed to load admin overview',
        }),
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadStats();
  }, []);

  return (
    <section className="page-shell">
      <div className="page-shell__header">
        <div className="stack stack--tight">
          <p className="eyebrow">Admin only</p>
          <h1>Admin panel</h1>
          <p className="page-note">This overview comes from the dedicated backend admin controller and is intended as the dashboard entry point.</p>
        </div>
        <div className="hero__actions">
          <button className="button button--ghost" type="button" onClick={() => void loadStats()} disabled={loading}>
            Refresh stats
          </button>
          <Link className="button" to="/admin/users">
            Open user directory
          </Link>
        </div>
      </div>

      {loading ? <div className="page-state">Loading admin overview...</div> : null}
      {error ? <p className="form__error">{error}</p> : null}

      <div className="grid grid--cards">
        <article className="card stack stack--tight">
          <p className="eyebrow">Total users</p>
          <h2>{stats.totalUsers}</h2>
          <p className="page-note">All accounts stored in the backend users table.</p>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">Active users</p>
          <h2>{stats.activeUsers}</h2>
          <p className="page-note">Regular users currently marked as active.</p>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">Inactive users</p>
          <h2>{stats.inactiveUsers}</h2>
          <p className="page-note">Accounts blocked or deactivated for now.</p>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">New this week</p>
          <h2>{stats.newUsersLast7Days}</h2>
          <p className="page-note">Users created in the last seven days.</p>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">Admin users</p>
          <h2>{stats.adminUsers}</h2>
          <p className="page-note">Users with the Admin role in the system.</p>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">Active admins</p>
          <h2>{stats.activeAdminUsers}</h2>
          <p className="page-note">Admin accounts that are currently active.</p>
        </article>
      </div>

      <div className="grid grid--2">
        <article className="card stack stack--tight">
          <p className="eyebrow">User management</p>
          <h2>Working area</h2>
          <p className="page-note">
            Use the user directory to manage roles, activation state, deletion, and detailed inspection of accounts.
          </p>
          <Link className="button button--ghost" to="/admin/users">
            Go to user directory
          </Link>
        </article>

        <article className="card stack stack--tight">
          <p className="eyebrow">Starter scope</p>
          <h2>Minimal but extendable</h2>
          <p className="page-note">
            The current admin slice keeps the backend contract visible while leaving user editing and address management for a later iteration.
          </p>
        </article>
      </div>
    </section>
  );
}