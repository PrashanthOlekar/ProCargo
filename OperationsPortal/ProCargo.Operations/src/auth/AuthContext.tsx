import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Alert, Box, CircularProgress, Container } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
import { onSessionChange, refreshSession, setAccessToken } from '../api/client';
import { authApi } from '../api/endpoints';
import type { CurrentUser } from '../api/types';

/** Permission codes used by the Operations portal (mirror of ProCargo.Domain.Constants.Permissions). */
export const P = {
  ManageUsers: 'ManageUsers',
  ManageRoles: 'ManageRoles',
  ViewCustomers: 'ViewCustomers',
  ManageCustomers: 'ManageCustomers',
  ViewOwners: 'ViewOwners',
  ManageOwners: 'ManageOwners',
  ApproveOwners: 'ApproveOwners',
  ViewDrivers: 'ViewDrivers',
  ManageDrivers: 'ManageDrivers',
  ApproveDrivers: 'ApproveDrivers',
  ViewVehicles: 'ViewVehicles',
  ManageVehicles: 'ManageVehicles',
  ApproveVehicles: 'ApproveVehicles',
  ViewBookings: 'ViewBookings',
  ManageBookings: 'ManageBookings',
  ManageQuotations: 'ManageQuotations',
  ApproveQuotations: 'ApproveQuotations',
  ViewTrips: 'ViewTrips',
  AssignTrips: 'AssignTrips',
  UpdateTrips: 'UpdateTrips',
  ManagePricing: 'ManagePricing',
  ViewFinance: 'ViewFinance',
  ManageInvoices: 'ManageInvoices',
  ManagePayments: 'ManagePayments',
  ManageRefunds: 'ManageRefunds',
  ManageSettlements: 'ManageSettlements',
  ApproveSettlements: 'ApproveSettlements',
  ViewReports: 'ViewReports',
  ManageSupport: 'ManageSupport',
  ManageComplaints: 'ManageComplaints',
  ManageMasterData: 'ManageMasterData',
  ManageSystemSettings: 'ManageSystemSettings',
  ManageNotificationTemplates: 'ManageNotificationTemplates',
  ViewAuditLogs: 'ViewAuditLogs',
} as const;

interface AuthState {
  user: CurrentUser | null;
  initializing: boolean;
  signIn: (email: string, password: string) => Promise<CurrentUser>;
  signOut: () => Promise<void>;
  can: (...permissions: string[]) => boolean;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [initializing, setInitializing] = useState(true);
  const queryClient = useQueryClient();

  useEffect(() => {
    refreshSession().finally(() => setInitializing(false));
    return onSessionChange((auth) => setUser(auth?.user ?? null));
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const auth = await authApi.login(email, password);
    setAccessToken(auth.accessToken);
    setUser(auth.user);
    return auth.user;
  }, []);

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
      signIn,
      signOut,
      can: (...permissions) => !!user && permissions.some((p) => user.permissions.includes(p)),
    }),
    [user, initializing, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

export function FullPageSpinner() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '60vh' }} role="status" aria-label="Loading">
      <CircularProgress />
    </Box>
  );
}

/** Signed-in staff only; an optional list of permissions (any of) gates the section. The API re-checks everything. */
export function RequireStaff({ anyOf }: { anyOf?: string[] }) {
  const { user, initializing, can } = useAuth();
  const location = useLocation();
  if (initializing) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  if (user.mustChangePassword && location.pathname !== '/account/password') return <Navigate to="/account/password" replace />;
  if (anyOf && !can(...anyOf)) {
    return (
      <Container sx={{ py: 6 }}>
        <Alert severity="warning">Your role does not include access to this area. Ask an administrator if you need it.</Alert>
      </Container>
    );
  }
  return <Outlet />;
}
