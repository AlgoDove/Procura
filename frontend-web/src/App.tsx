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

      {/* Protected routes — any authenticated user */}
      <Route element={<ProtectedRoute />}>
        <Route
          path="/*"
          element={
            <AppShell>
              <Routes>
                <Route path="/" element={<RootRedirect />} />
                <Route path="/requests" element={<RequestsListPage />} />
                <Route path="/requests/new" element={<CreateRequestPage />} />
                <Route path="/requests/:id" element={<RequestDetailPage />} />
                <Route path="/requests/:id/edit" element={<EditRequestPage />} />
                <Route path="/requests/:id/ai" element={<AiWorkflowPage />} />

                {/* Vendor routes — accessible to all authenticated (read), managed by PO/ADMIN */}
                <Route path="/vendors" element={<VendorsListPage />} />
                <Route path="/vendors/:id" element={<VendorDetailPage />} />
                <Route
                  element={<ProtectedRoute allowedRoles={['PROCUREMENT_OFFICER', 'ADMIN']} />}
                >
                  <Route path="/vendors/new" element={<CreateVendorPage />} />
                  <Route path="/vendors/:id/edit" element={<EditVendorPage />} />
                </Route>
              </Routes>
            </AppShell>
          }
        />
      </Route>
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
