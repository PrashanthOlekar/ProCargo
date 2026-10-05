import { useState } from 'react';
import { Link as RouterLink, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Container, Grid, Link, Stack, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { authApi, notificationApi, reportApi } from '../api/endpoints';
import type { NotificationItem } from '../api/types';
import { useAuth, P } from '../auth/AuthContext';
import { applyApiErrors, FormField, useNotify } from '../components/Forms';
import { EmptyState, PageHeader, QueryView, Section, StatTile } from '../components/Layout';
import { formatMoney, fromNow } from '../lib/format';
import * as v from '../lib/validation';
import { palette } from '../theme';
import { Wordmark } from '../components/Shell';

function AuthFrame({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: palette.asphalt, display: 'grid', placeItems: 'center', p: 2 }}>
      <Container maxWidth="xs" disableGutters>
        <Box sx={{ mb: 3 }}>
          <Wordmark />
        </Box>
        <Card>
          <CardContent sx={{ p: 4 }}>
            <Typography variant="h4" component="h1">
              {title}
            </Typography>
            {subtitle && (
              <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>
                {subtitle}
              </Typography>
            )}
            {children}
          </CardContent>
        </Card>
        <Typography variant="body2" sx={{ color: '#8D979E', mt: 2 }}>
          For ProCargo staff only. Activity on this portal is logged.
        </Typography>
      </Container>
    </Box>
  );
}

const loginSchema = z.object({ email: v.email, password: z.string().min(1, 'Enter your password') });

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof loginSchema>>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  });

  const submit = handleSubmit(async ({ email, password }) => {
    setError('');
    try {
      const user = await signIn(email, password);
      const from = (location.state as { from?: string } | null)?.from;
      navigate(user.mustChangePassword ? '/account/password' : from ?? '/', { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <AuthFrame title="Sign in to Operations" subtitle="Use your ProCargo staff account.">
      <Box component="form" onSubmit={submit} noValidate>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Stack spacing={2}>
          <FormField control={control} name="email" label="Work e-mail" type="email" autoComplete="username" autoFocus />
          <FormField control={control} name="password" label="Password" type="password" autoComplete="current-password" />
        </Stack>
        <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          {formState.isSubmitting ? 'Signing in…' : 'Sign in'}
        </Button>
        <Link component={RouterLink} to="/forgot-password" sx={{ display: 'inline-block', mt: 2 }}>
          Forgot password?
        </Link>
      </Box>
    </AuthFrame>
  );
}

export function ForgotPasswordPage() {
  const [sent, setSent] = useState('');
  const { control, handleSubmit, formState } = useForm<{ email: string }>({ resolver: zodResolver(z.object({ email: v.email })), defaultValues: { email: '' } });
  return (
    <AuthFrame title="Reset your password" subtitle="We'll e-mail a link to your work address.">
      {sent ? (
        <Alert severity="success">{sent}</Alert>
      ) : (
        <Box component="form" onSubmit={handleSubmit(async ({ email }) => setSent((await authApi.forgotPassword(email)).message))} noValidate>
          <FormField control={control} name="email" label="Work e-mail" type="email" autoFocus />
          <Button type="submit" variant="contained" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
            Send reset link
          </Button>
        </Box>
      )}
      <Link component={RouterLink} to="/login" sx={{ display: 'inline-block', mt: 2 }}>
        Back to sign in
      </Link>
    </AuthFrame>
  );
}

const resetSchema = z
  .object({ newPassword: v.password, confirmPassword: z.string() })
  .refine((d) => d.newPassword === d.confirmPassword, { path: ['confirmPassword'], message: 'The passwords do not match' });

/** Also used by the invitation link new staff receive. */
export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof resetSchema>>({
    resolver: zodResolver(resetSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });
  const submit = handleSubmit(async ({ newPassword }) => {
    setError('');
    try {
      notify((await authApi.resetPassword(params.get('token') ?? '', newPassword)).message);
      navigate('/login', { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });
  return (
    <AuthFrame title="Set your password" subtitle="At least 8 characters with upper and lower case, a number and a symbol.">
      <Box component="form" onSubmit={submit} noValidate>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Stack spacing={2}>
          <FormField control={control} name="newPassword" label="New password" type="password" autoComplete="new-password" autoFocus />
          <FormField control={control} name="confirmPassword" label="Repeat new password" type="password" autoComplete="new-password" />
        </Stack>
        <Button type="submit" variant="contained" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          Save password
        </Button>
      </Box>
    </AuthFrame>
  );
}

const changeSchema = z
  .object({ currentPassword: z.string().min(1, 'Enter your current password'), newPassword: v.password, confirmPassword: z.string() })
  .refine((d) => d.newPassword === d.confirmPassword, { path: ['confirmPassword'], message: 'The passwords do not match' });

export function ChangePasswordPage() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof changeSchema>>({
    resolver: zodResolver(changeSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });
  const submit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setError('');
    try {
      notify((await authApi.changePassword(currentPassword, newPassword)).message);
      await signOut();
      navigate('/login', { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });
  return (
    <AuthFrame title={user?.mustChangePassword ? 'Choose your own password' : 'Change password'} subtitle="You will be signed out everywhere afterwards.">
      <Box component="form" onSubmit={submit} noValidate>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Stack spacing={2}>
          <FormField control={control} name="currentPassword" label="Current password" type="password" autoComplete="current-password" />
          <FormField control={control} name="newPassword" label="New password" type="password" autoComplete="new-password" />
          <FormField control={control} name="confirmPassword" label="Repeat new password" type="password" autoComplete="new-password" />
        </Stack>
        <Button type="submit" variant="contained" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          Change password
        </Button>
        {!user?.mustChangePassword && (
          <Button component={RouterLink} to="/" fullWidth sx={{ mt: 1 }}>
            Back to the desk
          </Button>
        )}
      </Box>
    </AuthFrame>
  );
}

/** Desk overview: what needs a person's attention now, then the month's numbers. */
export function DashboardPage() {
  const { user, can } = useAuth();
  const query = useQuery({ queryKey: ['dashboard'], queryFn: reportApi.dashboard, refetchInterval: 60_000 });
  const navigate = useNavigate();

  return (
    <>
      <PageHeader title={`Good ${new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 17 ? 'afternoon' : 'evening'}, ${user?.fullName.split(' ')[0]}`} subtitle="Queues that need someone from the desk." />
      <QueryView query={query}>
        {(d) => {
          const queues = [
            { label: 'Bookings to review', value: d.pendingBookings, to: '/bookings?status=2', show: can(P.ManageBookings, P.ManageQuotations) },
            { label: 'Confirmed, need a truck', value: d.awaitingAssignment, to: '/bookings?status=5', show: can(P.AssignTrips) },
            { label: 'Trip exceptions', value: d.tripExceptions, to: '/trips?status=10', show: can(P.UpdateTrips), urgent: true },
            { label: 'Quotations with customers', value: d.pendingQuotations, to: '/quotations?status=2', show: can(P.ManageQuotations) },
            { label: 'Owners to verify', value: d.pendingOwnerApprovals, to: '/owners?verification=Pending', show: can(P.ApproveOwners) },
            { label: 'Vehicles to verify', value: d.pendingVehicleApprovals, to: '/vehicles?verification=Pending', show: can(P.ApproveVehicles) },
            { label: 'Drivers to verify', value: d.pendingDriverApprovals, to: '/drivers?verification=Pending', show: can(P.ApproveDrivers) },
            { label: 'Vehicle documents expiring in 30 days', value: d.vehiclesWithExpiringDocuments, to: '/vehicles?expiring=1', show: can(P.ViewVehicles) },
            { label: 'Settlements to process', value: d.pendingSettlements, to: '/settlements', show: can(P.ManageSettlements, P.ApproveSettlements) },
            { label: 'Open support tickets', value: d.openTickets, to: '/support/tickets', show: can(P.ManageSupport) },
            { label: 'Open complaints', value: d.openComplaints, to: '/support/complaints', show: can(P.ManageComplaints) },
          ].filter((q) => q.show);

          return (
            <>
              <Box
                component="ul"
                sx={{ listStyle: 'none', p: 0, m: 0, mb: 3, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)' }, border: 1, borderColor: 'divider', borderRadius: 1.5, bgcolor: 'background.paper', overflow: 'hidden' }}
              >
                {queues.map((q) => (
                  <Box
                    component="li"
                    key={q.label}
                    onClick={() => navigate(q.to)}
                    onKeyDown={(e) => e.key === 'Enter' && navigate(q.to)}
                    tabIndex={0}
                    sx={{ p: 2.25, borderRight: 1, borderBottom: 1, borderColor: 'divider', mr: '-1px', mb: '-1px', cursor: 'pointer', '&:hover': { bgcolor: '#F6F8F7' }, display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: 2 }}
                  >
                    <Typography color={q.value > 0 ? 'text.primary' : 'text.secondary'}>{q.label}</Typography>
                    <Typography sx={{ fontSize: '1.9rem', fontWeight: 800, fontStretch: '75%', color: q.value > 0 ? (q.urgent ? 'error.main' : palette.tealDark) : 'text.disabled' }}>{q.value}</Typography>
                  </Box>
                ))}
              </Box>

              <Grid container spacing={2}>
                <Grid size={{ xs: 6, md: 3 }}>
                  <StatTile label="Bookings today" value={d.todaysBookings} hint={`${d.totalBookings} all time`} />
                </Grid>
                <Grid size={{ xs: 6, md: 3 }}>
                  <StatTile label="Trips on the road" value={d.activeTrips} hint={`${d.completedTrips} completed`} />
                </Grid>
                {can(P.ViewFinance) && (
                  <>
                    <Grid size={{ xs: 6, md: 3 }}>
                      <StatTile label="Invoiced this month" value={formatMoney(d.revenueThisMonth)} hint={`${formatMoney(d.collectionsThisMonth)} collected`} />
                    </Grid>
                    <Grid size={{ xs: 6, md: 3 }}>
                      <StatTile label="Customers owe" value={formatMoney(d.outstandingAmount)} hint={`${d.unpaidInvoices} unpaid invoices`} />
                    </Grid>
                  </>
                )}
              </Grid>
            </>
          );
        }}
      </QueryView>
    </>
  );
}

export function NotificationsPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const query = useQuery({ queryKey: ['notifications', 'list'], queryFn: () => notificationApi.list({ pageSize: 50 }) });
  const links: Record<string, string> = { Booking: '/bookings/', Trip: '/trips/', Invoice: '/invoices/', Settlement: '/settlements/', Quotation: '/quotations/', SupportTicket: '/support/tickets/' };

  const open = async (n: NotificationItem) => {
    if (!n.readDateUtc) {
      await notificationApi.markRead(n.notificationId).catch(() => undefined);
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    }
    if (n.entityType && n.entityId && links[n.entityType]) navigate(`${links[n.entityType]}${n.entityId}`);
  };

  return (
    <>
      <PageHeader
        title="Notifications"
        actions={<Button onClick={async () => { await notificationApi.markAllRead(); queryClient.invalidateQueries({ queryKey: ['notifications'] }); }}>Mark all as read</Button>}
      />
      <Section>
        <QueryView query={query}>
          {(page) =>
            page.items.length === 0 ? (
              <EmptyState title="Nothing new" />
            ) : (
              <Stack divider={<Box sx={{ borderTop: 1, borderColor: 'divider' }} />}>
                {page.items.map((n) => (
                  <Box key={n.notificationId} onClick={() => open(n)} sx={{ py: 1.5, cursor: 'pointer' }}>
                    <Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}>
                      <Typography sx={{ fontWeight: n.readDateUtc ? 500 : 750 }}>{n.title}</Typography>
                      <Typography variant="body2" color="text.secondary">
                        {fromNow(n.createdDateUtc)}
                      </Typography>
                    </Stack>
                    <Typography variant="body2" color="text.secondary">
                      {n.message}
                    </Typography>
                  </Box>
                ))}
              </Stack>
            )
          }
        </QueryView>
      </Section>
    </>
  );
}
