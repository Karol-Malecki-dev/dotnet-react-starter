import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';

export function Navbar() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const isAdmin = user?.role === 'Admin';

  const handleLogout = async () => {
    await logout();
    navigate('/');
  };

  return (
    <header className="navbar">
      <div className="navbar__brand">
        <span className="navbar__logo">DRS</span>
        <div>
          <strong>dotnet-react-starter</strong>
          <p>JWT-ready frontend</p>
        </div>
      </div>

      <nav className="navbar__links">
        <NavLink to="/">Home</NavLink>
        {isAuthenticated ? <NavLink to="/dashboard">Dashboard</NavLink> : null}
        {isAuthenticated ? <NavLink to="/profile">Profile</NavLink> : null}
        {isAdmin ? <NavLink to="/admin">Admin</NavLink> : null}
        {isAdmin ? <NavLink to="/admin/users">Users</NavLink> : null}
      </nav>

      <div className="navbar__actions">
        {isAuthenticated && user ? (
          <>
            <span className="navbar__user">
              {user.displayName}
              <span className={`role-badge ${isAdmin ? 'role-badge--admin' : ''}`}>{user.role}</span>
            </span>
            <button type="button" className="button button--ghost" onClick={handleLogout}>
              Logout
            </button>
          </>
        ) : (
          <>
            <NavLink className="button button--ghost" to="/login">
              Login
            </NavLink>
            <NavLink className="button" to="/register">
              Register
            </NavLink>
          </>
        )}
      </div>
    </header>
  );
}
