import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { onSessionChange, refreshSession, setAccessToken } from '../api/client';
import { authApi } from '../api/endpoints';
import type { AuthResponse, CurrentUser } from '../api/types';

export type PartnerRole = 'customer' | 'owner' | 'driver';

interface AuthState {
  user: CurrentUser | null;
  /** True until the silent refresh on page load has finished. */
  initializing: boolean;
  role: PartnerRole | null;
  signIn: (email: string, password: string) => Promise<CurrentUser>;
  completeSignIn: (auth: AuthResponse) => CurrentUser;
  signOut: () => Promise<void>;
  can: (permission: string) => boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function roleOf(user: CurrentUser | null): PartnerRole | null {
  if (!user) return null;
  if (user.driverId) return 'driver';
  if (user.ownerId) return 'owner';
  if (user.customerId) return 'customer';
  return null;
}

export const homePathFor = (user: CurrentUser) => {
  if (user.mustChangePassword) return '/account/password';
  const role = roleOf(user);
  return role ? `/${role}` : '/';
};

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [initializing, setInitializing] = useState(true);
  const queryClient = useQueryClient();

  useEffect(() => {
    // Restore the session from the HttpOnly refresh cookie (if any) when the app loads.
    refreshSession().finally(() => setInitializing(false));
    return onSessionChange((auth) => setUser(auth?.user ?? null));
  }, []);

  const completeSignIn = useCallback((auth: AuthResponse) => {
    setAccessToken(auth.accessToken);
    setUser(auth.user);
    return auth.user;
  }, []);

  const signIn = useCallback(
    async (email: string, password: string) => completeSignIn(await authApi.login(email, password)),
    [completeSignIn],
  );

  const signOut = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      setAccessToken(null);
      setUser(null);
      queryClient.clear();
    }
  }, [queryClient]);

  const value = useMemo<AuthState>(
    () => ({
      user,
      initializing,
      role: roleOf(user),
      signIn,
      completeSignIn,
      signOut,
      can: (permission) => !!user?.permissions.includes(permission),
    }),
    [user, initializing, signIn, completeSignIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
