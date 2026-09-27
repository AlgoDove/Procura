import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuth } from './context/AuthContext';
import AppShell from './components/AppShell';
import ProtectedRoute from './components/ProtectedRoute';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import RequestsListPage from './pages/RequestsListPage';
import RequestDetailPage from './pages/RequestDetailPage';
import CreateRequestPage from './pages/CreateRequestPage';
import EditRequestPage from './pages/EditRequestPage';
import AiWorkflowPage from './pages/AiWorkflowPage';
import AdminUsersPage from './pages/AdminUsersPage';
import VendorsListPage from './pages/VendorsListPage';
import VendorDetailPage from './pages/VendorDetailPage';
import CreateVendorPage from './pages/CreateVendorPage';
import EditVendorPage from './pages/EditVendorPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000,
    },
  },
});

function RootRedirect() {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  return <Navigate to="/requests" replace />;
}

function AppRoutes() {
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Root path: unauthenticated -> /login, authenticated -> /requests */}
      <Route path="/" element={<RootRedirect />} />

      {/* Protected routes — any authenticated user */}
      <Route element={<ProtectedRoute />}>
        <Route
          path="/*"
          element={
            <AppShell>
              <Routes>
                <Route path="/requests" element={<RequestsListPage />} />
                <Route
                  element={<ProtectedRoute allowedRoles={['EMPLOYEE', 'ADMIN']} />}
                >
                  <Route path="/requests/new" element={<CreateRequestPage />} />
                  <Route path="/requests/:id/edit" element={<EditRequestPage />} />
                  <Route path="/requests/:id/ai" element={<AiWorkflowPage />} />
                </Route>
                <Route path="/requests/:id" element={<RequestDetailPage />} />

                {/* Vendor routes — accessible only to PO and ADMIN */}
                <Route
                  element={<ProtectedRoute allowedRoles={['PROCUREMENT_OFFICER', 'ADMIN']} />}
                >
                  <Route path="/vendors" element={<VendorsListPage />} />
                  <Route path="/vendors/new" element={<CreateVendorPage />} />
                  <Route path="/vendors/:id" element={<VendorDetailPage />} />
                  <Route path="/vendors/:id/edit" element={<EditVendorPage />} />
                </Route>

                {/* Admin routes — accessible only to ADMIN */}
                <Route
                  element={<ProtectedRoute allowedRoles={['ADMIN']} />}
                >
                  <Route path="/admin" element={<AdminUsersPage />} />
                </Route>

                {/* Inside AppShell fallback */}
                <Route path="*" element={<Navigate to="/" replace />} />
              </Routes>
            </AppShell>
          }
        />
      </Route>

      {/* Fallback for unmatched routes */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <BrowserRouter>
          <AppRoutes />
        </BrowserRouter>
      </AuthProvider>
    </QueryClientProvider>
  );
}
