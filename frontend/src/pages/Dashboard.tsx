import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export default function Dashboard() {
  const { user, tokens, isAuthenticated } = useAuth();
  const isAdmin = user?.role === 'Admin';

  return (
    <section className="page-shell">
      <h1>Dashboard</h1>
      <p>Protected workspace for authenticated users with role-aware navigation.</p>

      <div className="grid grid--2">
        <article className="card">
          <h2>Session</h2>
          <p>Status: {isAuthenticated ? 'authenticated' : 'anonymous'}</p>
          <p>Access token expires in: {tokens?.expiresIn ?? 0}s</p>
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
    </section>
  );
}
