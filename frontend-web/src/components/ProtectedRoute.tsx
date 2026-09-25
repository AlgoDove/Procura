import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import type { SystemRole } from '../types/api';

interface ProtectedRouteProps {
  allowedRoles?: SystemRole[];
}

/**
 * Redirects unauthenticated users to /login.
 * Optionally enforces role-based access (403 redirect to /).
 */
export default function ProtectedRoute({ allowedRoles }: ProtectedRouteProps) {
  const { user } = useAuth();

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
