import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { AppNoticeCenter } from './components/UI/AppNoticeCenter';
import { Navbar } from './components/UI/Navbar';
import { ProtectedRoute } from './components/UI/ProtectedRoute';
import AdminPanel from './pages/AdminPanel';
import ConfirmEmail from './pages/ConfirmEmail';
import Dashboard from './pages/Dashboard';
import Forbidden from './pages/Forbidden';
import ForgotPassword from './pages/ForgotPassword';
import Home from './pages/Home';
import Login from './pages/Login';
import NotFound from './pages/NotFound';
import Profile from './pages/Profile';
import Register from './pages/Register';
import ResetPassword from './pages/ResetPassword';
import VerifyTwoFactor from './pages/VerifyTwoFactor';
import UserList from './pages/users/UserList';

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <div className="app-shell">
          <AppNoticeCenter />
          <Navbar />
          <main className="app-shell__main">
            <Routes>
              <Route path="/" element={<Home />} />
              <Route path="/login" element={<Login />} />
              <Route path="/register" element={<Register />} />
              <Route path="/confirm-email" element={<ConfirmEmail />} />
              <Route path="/forgot-password" element={<ForgotPassword />} />
              <Route path="/reset-password" element={<ResetPassword />} />
              <Route path="/verify-2fa" element={<VerifyTwoFactor />} />

              <Route element={<ProtectedRoute />}>
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/profile" element={<Profile />} />
              </Route>

              <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
                <Route path="/admin" element={<AdminPanel />} />
                <Route path="/admin/users" element={<UserList />} />
                <Route path="/users" element={<Navigate to="/admin/users" replace />} />
              </Route>

              <Route path="/forbidden" element={<Forbidden />} />
              <Route path="/home" element={<Navigate to="/" replace />} />
              <Route path="*" element={<NotFound />} />
            </Routes>
          </main>
        </div>
      </AuthProvider>
    </BrowserRouter>
  );
}
