import { Navigate, Route, Routes } from 'react-router-dom';
import { useFeatureAvailability } from '../hooks/useFeatureAvailability';
import AdminPanel from '../pages/AdminPanel';
import ConfirmEmail from '../pages/ConfirmEmail';
import Dashboard from '../pages/Dashboard';
import Forbidden from '../pages/Forbidden';
import ForgotPassword from '../pages/ForgotPassword';
import Home from '../pages/Home';
import Login from '../pages/Login';
import NotFound from '../pages/NotFound';
import Notifications from '../pages/Notifications';
import Profile from '../pages/Profile';
import ProjectInvitation from '../pages/ProjectInvitation';
import Projects from '../pages/Projects';
import Register from '../pages/Register';
import ResetPassword from '../pages/ResetPassword';
import VerifyTwoFactor from '../pages/VerifyTwoFactor';
import UserList from '../pages/users/UserList';
import { ProtectedRoute } from './UI/ProtectedRoute';
import { ProjectsProvider } from '../context/ProjectsContext';

export function AppRoutes() {
  const { dashboardOverviewEnabled, projectsEnabled, adminNavigationEnabled, userManagementNavigationEnabled, emailTwoFactorEnabled } = useFeatureAvailability();

  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/confirm-email" element={<ConfirmEmail />} />
      <Route path="/forgot-password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ResetPassword />} />
      <Route
        path="/verify-2fa"
        element={emailTwoFactorEnabled ? <VerifyTwoFactor /> : <Navigate to="/login" replace />}
      />

      <Route element={<ProtectedRoute />}>
        <Route path="/dashboard" element={dashboardOverviewEnabled ? <Dashboard /> : <Navigate to="/" replace />} />
        <Route path="/profile" element={<Profile />} />
        <Route path="/notifications" element={<Notifications />} />
        <Route path="/projects" element={projectsEnabled ? <ProjectsProvider><Projects /></ProjectsProvider> : <Navigate to="/" replace />} />
        <Route path="/project-invitation" element={projectsEnabled ? <ProjectsProvider><ProjectInvitation /></ProjectsProvider> : <Navigate to="/" replace />} />
      </Route>

      <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
        <Route path="/admin" element={adminNavigationEnabled ? <AdminPanel /> : <Navigate to="/" replace />} />
        <Route
          path="/admin/users"
          element={userManagementNavigationEnabled ? <UserList /> : <Navigate to="/admin" replace />}
        />
        <Route
          path="/users"
          element={userManagementNavigationEnabled ? <Navigate to="/admin/users" replace /> : <Navigate to="/admin" replace />}
        />
      </Route>

      <Route path="/forbidden" element={<Forbidden />} />
      <Route path="/home" element={<Navigate to="/" replace />} />
      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}