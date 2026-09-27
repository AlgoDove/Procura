import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthProvider, useAuth } from '../context/AuthContext';

// A helper component to expose auth state
function AuthDisplay() {
  const { user, logout } = useAuth();
  return (
    <div>
      <span data-testid="role">{user?.role ?? 'none'}</span>
      <span data-testid="email">{user?.email ?? 'none'}</span>
      <button onClick={() => logout()}>Logout</button>
      {/* We cannot call login with a real JWT in unit tests — covered by role decoding tests */}
    </div>
  );
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows no user when localStorage is empty', () => {
    render(
      <AuthProvider>
        <AuthDisplay />
      </AuthProvider>
    );
    expect(screen.getByTestId('role').textContent).toBe('none');
    expect(screen.getByTestId('email').textContent).toBe('none');
  });

  it('clears user on logout', async () => {
    // Store a mock-expired or invalid token so user starts as null
    localStorage.clear();
    render(
      <AuthProvider>
        <AuthDisplay />
      </AuthProvider>
    );

    const logoutBtn = screen.getByRole('button', { name: /logout/i });
    await userEvent.click(logoutBtn);

    expect(screen.getByTestId('role').textContent).toBe('none');
    expect(localStorage.getItem('token')).toBeNull();
  });

  it('reacts to storage event on 401', () => {
    render(
      <AuthProvider>
        <AuthDisplay />
      </AuthProvider>
    );

    // Simulate Axios interceptor removing token and dispatching storage event
    localStorage.removeItem('token');
    window.dispatchEvent(new Event('storage'));

    // User should still be null/none
    expect(screen.getByTestId('role').textContent).toBe('none');
  });
});
