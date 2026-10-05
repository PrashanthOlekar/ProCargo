import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { homePathFor, useAuth, type PartnerRole } from './AuthContext';

export function FullPageSpinner() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '60vh' }} role="status" aria-label="Loading">
      <CircularProgress />
    </Box>
  );
}

/** Route guard: signed in, password changed, and the right partner role. Everything is re-checked by the API. */
export function RequireRole({ role }: { role: PartnerRole }) {
  const { user, initializing, role: current } = useAuth();
  const location = useLocation();

  if (initializing) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  if (user.mustChangePassword) return <Navigate to="/account/password" replace />;
  if (current !== role) return <Navigate to={homePathFor(user)} replace />;
  return <Outlet />;
}

export function RequireSignedIn() {
  const { user, initializing } = useAuth();
  const location = useLocation();
  if (initializing) return <FullPageSpinner />;
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}
