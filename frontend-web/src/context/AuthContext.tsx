import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { jwtDecode } from 'jwt-decode';
import type { JwtPayload, SystemRole } from '../types/api';

interface AuthUser {
  userId: string;
  email: string;
  role: SystemRole;
}

interface AuthContextValue {
  user: AuthUser | null;
  token: string | null;
  isLoading: boolean;
  login: (token: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function decodeToken(token: string): AuthUser | null {
  try {
    const payload = jwtDecode<JwtPayload>(token);
    // Reject expired tokens immediately
    if (payload.exp * 1000 < Date.now()) return null;
    return {
      userId: payload.sub,
      email: payload.email,
      // The backend sets both "role" and ClaimTypes.Role.
      // jwt-decode maps the raw claim key from the JWT, which is "role".
      role: payload.role as SystemRole,
    };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('token'));
  const [user, setUser] = useState<AuthUser | null>(() => {
    const stored = localStorage.getItem('token');
    return stored ? decodeToken(stored) : null;
  });
  const [isLoading] = useState(false);

  const login = useCallback((newToken: string) => {
    const decoded = decodeToken(newToken);
    if (!decoded) return;
    localStorage.setItem('token', newToken);
    setToken(newToken);
    setUser(decoded);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('token');
    setToken(null);
    setUser(null);
  }, []);

  // React to 401 from Axios interceptor (fires a 'storage' event)
  useEffect(() => {
    const handleStorage = () => {
      const stored = localStorage.getItem('token');
      if (!stored) {
        setToken(null);
        setUser(null);
      }
    };
    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, []);

  return (
    <AuthContext.Provider value={{ user, token, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
