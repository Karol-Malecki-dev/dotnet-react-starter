import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { useFeatureAvailability } from '../../hooks/useFeatureAvailability';
import { QuickSearchBar, type QuickSearchItem } from './QuickSearchBar';
import { NotificationBell } from './NotificationBell';

export function Navbar() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const {
    globalSearchEnabled,
    dashboardOverviewEnabled,
    adminNavigationEnabled,
    userManagementNavigationEnabled,
    emailTwoFactorEnabled,
    projectsEnabled,
  } = useFeatureAvailability();
  const isAdmin = user?.role === 'Admin';

  const searchItems: QuickSearchItem[] = [
    { label: 'Home', description: 'Landing page and auth overview', to: '/' },
    ...(isAuthenticated && dashboardOverviewEnabled ? [{ label: 'Dashboard', description: 'Protected workspace summary', to: '/dashboard' }] : []),
    ...(isAuthenticated && projectsEnabled ? [{ label: 'Projects', description: 'Manage projects and tasks', to: '/projects' }] : []),
    ...(isAuthenticated ? [{ label: 'Profile', description: 'Manage your account details', to: '/profile' }] : []),
    ...(isAdmin && adminNavigationEnabled ? [{ label: 'Admin panel', description: 'Administration overview', to: '/admin' }] : []),
    ...(isAdmin && userManagementNavigationEnabled ? [{ label: 'Users directory', description: 'Search and manage users', to: '/admin/users', keywords: ['users', 'people'] }] : []),
    ...(emailTwoFactorEnabled ? [{ label: 'Two-factor verification', description: 'Complete the 2FA challenge', to: '/verify-2fa' }] : []),
    ...(!isAuthenticated ? [
      { label: 'Login', description: 'Sign in to continue', to: '/login' },
      { label: 'Register', description: 'Create a new account', to: '/register' },
    ] : []),
  ];

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

      {globalSearchEnabled ? (
        <QuickSearchBar
          items={searchItems}
          placeholder="Search pages, users, and actions"
          label="Project search"
        />
      ) : null}

      <nav className="navbar__links">
        <NavLink to="/">Home</NavLink>
        {isAuthenticated && dashboardOverviewEnabled ? <NavLink to="/dashboard">Dashboard</NavLink> : null}
        {isAuthenticated && projectsEnabled ? <NavLink to="/projects">Projects</NavLink> : null}
        {isAuthenticated ? <NavLink to="/profile">Profile</NavLink> : null}
        {isAdmin && adminNavigationEnabled ? <NavLink to="/admin">Admin</NavLink> : null}
        {isAdmin && userManagementNavigationEnabled ? <NavLink to="/admin/users">Users</NavLink> : null}
      </nav>

      <div className="navbar__actions">
        {isAuthenticated && user ? (
          <>
            <NotificationBell />
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
