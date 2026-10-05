import { useState } from 'react';
import { Link as RouterLink, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Container, Grid, Link, MenuItem, Stack, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { authApi, referenceApi } from '../../api/endpoints';
import type { AccountType } from '../../api/types';
import { homePathFor, useAuth } from '../../auth/AuthContext';
import { applyApiErrors, FormField, useNotify } from '../../components/Forms';
import * as v from '../../lib/validation';
import { palette } from '../../theme';

function AuthCard({ title, subtitle, children }: { title: string; subtitle?: React.ReactNode; children: React.ReactNode }) {
  return (
    <Container maxWidth="sm" sx={{ py: { xs: 4, md: 8 } }}>
      <Card>
        <CardContent sx={{ p: { xs: 3, md: 4.5 } }}>
          <Typography variant="h3" component="h1">
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
    </Container>
  );
}

// ---------------- sign in ----------------

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

  const from = (location.state as { from?: string } | null)?.from;

  const submit = handleSubmit(async ({ email, password }) => {
    setError('');
    try {
      const user = await signIn(email, password);
      navigate(from && !user.mustChangePassword ? from : homePathFor(user), { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <AuthCard title="Sign in" subtitle="Customers, truck owners and drivers all sign in here.">
      <Box component="form" onSubmit={submit} noValidate>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        <Stack spacing={2}>
          <FormField control={control} name="email" label="E-mail" type="email" autoComplete="username" autoFocus />
          <FormField control={control} name="password" label="Password" type="password" autoComplete="current-password" />
        </Stack>
        <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          {formState.isSubmitting ? 'Signing in…' : 'Sign in'}
        </Button>
        <Stack direction="row" sx={{ justifyContent: 'space-between', mt: 2 }}>
          <Link component={RouterLink} to="/forgot-password">
            Forgot password?
          </Link>
          <Link component={RouterLink} to="/register">
            Create an account
          </Link>
        </Stack>
      </Box>
    </AuthCard>
  );
}

// ---------------- register ----------------

const registerSchema = z
  .object({
    accountType: z.enum(['Customer', 'VehicleOwner', 'Driver']),
    fullName: v.personName,
    email: v.email,
    phoneNumber: v.phone,
    password: v.password,
    confirmPassword: z.string(),
    customerType: z.enum(['Individual', 'Business']),
    companyName: v.safeText(200).optional(),
    ownerType: z.enum(['Individual', 'FleetBusiness']),
    businessName: v.safeText(200).optional(),
    gstNumber: v.gst,
  })
  .refine((d) => d.password === d.confirmPassword, { path: ['confirmPassword'], message: 'The passwords do not match' })
  .refine((d) => !(d.accountType === 'Customer' && d.customerType === 'Business' && !d.companyName), {
    path: ['companyName'],
    message: 'Enter the company name',
  });

type RegisterForm = z.input<typeof registerSchema>;

const accountCopy: Record<AccountType, string> = {
  Customer: 'I need to move goods',
  VehicleOwner: 'I own trucks',
  Driver: 'I drive a truck',
};

export function RegisterPage() {
  const { completeSignIn } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [error, setError] = useState('');
  const initial = (params.get('as') as AccountType | null) ?? 'Customer';
  const { control, handleSubmit, watch, setValue, setError: setFieldError, formState } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      accountType: initial,
      fullName: '',
      email: '',
      phoneNumber: '',
      password: '',
      confirmPassword: '',
      customerType: 'Individual',
      companyName: '',
      ownerType: 'Individual',
      businessName: '',
      gstNumber: '',
    },
  });
  const accountType = watch('accountType');
  const customerType = watch('customerType');
  const ownerType = watch('ownerType');

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      const auth = await authApi.register({
        accountType: d.accountType,
        fullName: d.fullName,
        email: d.email,
        phoneNumber: d.phoneNumber,
        password: d.password,
        customerType: d.accountType === 'Customer' ? d.customerType : undefined,
        companyName: d.accountType === 'Customer' && d.customerType === 'Business' ? d.companyName : undefined,
        ownerType: d.accountType === 'VehicleOwner' ? d.ownerType : undefined,
        businessName: d.accountType === 'VehicleOwner' ? d.businessName || undefined : undefined,
        gstNumber: d.gstNumber || undefined,
      });
      navigate(homePathFor(completeSignIn(auth)), { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <AuthCard title="Create your account" subtitle="Takes a minute. Truck owners and drivers are verified before their first trip.">
      <Box component="form" onSubmit={submit} noValidate>
        <ToggleButtonGroup
          exclusive
          fullWidth
          value={accountType}
          onChange={(_, value: AccountType | null) => value && setValue('accountType', value)}
          aria-label="Account type"
          sx={{ mb: 3, flexWrap: { xs: 'wrap', sm: 'nowrap' } }}
        >
          {(Object.keys(accountCopy) as AccountType[]).map((t) => (
            <ToggleButton key={t} value={t} sx={{ textTransform: 'none', fontWeight: 650 }}>
              {accountCopy[t]}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Grid container spacing={2}>
          {accountType === 'Customer' && (
            <Grid size={12}>
              <FormField control={control} name="customerType" label="Booking as" select>
                <MenuItem value="Individual">An individual</MenuItem>
                <MenuItem value="Business">A business</MenuItem>
              </FormField>
            </Grid>
          )}
          {accountType === 'VehicleOwner' && (
            <Grid size={12}>
              <FormField control={control} name="ownerType" label="Ownership" select>
                <MenuItem value="Individual">I own one or two trucks</MenuItem>
                <MenuItem value="FleetBusiness">We run a fleet business</MenuItem>
              </FormField>
            </Grid>
          )}
          <Grid size={12}>
            <FormField control={control} name="fullName" label="Full name" autoComplete="name" />
          </Grid>
          {accountType === 'Customer' && customerType === 'Business' && (
            <Grid size={12}>
              <FormField control={control} name="companyName" label="Company name" autoComplete="organization" />
            </Grid>
          )}
          {accountType === 'VehicleOwner' && ownerType === 'FleetBusiness' && (
            <Grid size={12}>
              <FormField control={control} name="businessName" label="Business name" autoComplete="organization" />
            </Grid>
          )}
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="email" label="E-mail" type="email" autoComplete="email" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="phoneNumber" label="Mobile number" type="tel" inputMode="tel" autoComplete="tel" />
          </Grid>
          {accountType !== 'Driver' && (customerType === 'Business' || accountType === 'VehicleOwner') && (
            <Grid size={12}>
              <FormField control={control} name="gstNumber" label="GSTIN (optional)" helperText="Shown on your invoices" />
            </Grid>
          )}
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="password" label="Password" type="password" autoComplete="new-password" helperText="8+ characters with upper, lower, number and symbol" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="confirmPassword" label="Repeat password" type="password" autoComplete="new-password" />
          </Grid>
        </Grid>
        <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          {formState.isSubmitting ? 'Creating account…' : 'Create account'}
        </Button>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          Already registered?{' '}
          <Link component={RouterLink} to="/login">
            Sign in
          </Link>
        </Typography>
      </Box>
    </AuthCard>
  );
}

// ---------------- forgot / reset password ----------------

export function ForgotPasswordPage() {
  const [sent, setSent] = useState('');
  const { control, handleSubmit, formState } = useForm<{ email: string }>({
    resolver: zodResolver(z.object({ email: v.email })),
    defaultValues: { email: '' },
  });
  const [error, setError] = useState('');

  const submit = handleSubmit(async ({ email }) => {
    setError('');
    try {
      setSent((await authApi.forgotPassword(email)).message);
    } catch (e) {
      setError((e as Error).message);
    }
  });

  return (
    <AuthCard title="Reset your password" subtitle="We'll e-mail you a link that works for 30 minutes.">
      {sent ? (
        <Alert severity="success">{sent}</Alert>
      ) : (
        <Box component="form" onSubmit={submit} noValidate>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <FormField control={control} name="email" label="E-mail" type="email" autoComplete="username" autoFocus />
          <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
            Send reset link
          </Button>
        </Box>
      )}
      <Link component={RouterLink} to="/login" sx={{ display: 'inline-block', mt: 2 }}>
        Back to sign in
      </Link>
    </AuthCard>
  );
}

const resetSchema = z
  .object({ newPassword: v.password, confirmPassword: z.string() })
  .refine((d) => d.newPassword === d.confirmPassword, { path: ['confirmPassword'], message: 'The passwords do not match' });

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const notify = useNotify();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof resetSchema>>({
    resolver: zodResolver(resetSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  });

  const submit = handleSubmit(async ({ newPassword }) => {
    setError('');
    try {
      const result = await authApi.resetPassword(token, newPassword);
      notify(result.message);
      navigate('/login', { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  if (!token) {
    return (
      <AuthCard title="This link is incomplete">
        <Alert severity="warning">Open the link from the e-mail again, or request a new one.</Alert>
        <Button component={RouterLink} to="/forgot-password" sx={{ mt: 2 }}>
          Request a new link
        </Button>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Choose a new password" subtitle="Also used when you set the password for an account created for you.">
      <Box component="form" onSubmit={submit} noValidate>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}
        <Stack spacing={2}>
          <FormField control={control} name="newPassword" label="New password" type="password" autoComplete="new-password" autoFocus />
          <FormField control={control} name="confirmPassword" label="Repeat new password" type="password" autoComplete="new-password" />
        </Stack>
        <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
          Save password
        </Button>
      </Box>
    </AuthCard>
  );
}

// ---------------- partners & contact ----------------

export function PartnersPage() {
  const points = [
    ['Loads that match your truck', 'We offer you trips that fit your vehicle type and capacity — no broker calls, no haggling at the loading point.'],
    ['Paid to your bank account', 'Freight minus the platform commission and TDS, settled to your verified account after the customer pays.'],
    ['Your drivers, your fleet', 'Add drivers, keep RC, permit, insurance and fitness documents in one place and get reminders before they expire.'],
  ];
  return (
    <>
      <Box sx={{ bgcolor: palette.asphalt, color: '#fff', py: { xs: 6, md: 9 } }}>
        <Container maxWidth="lg">
          <Typography variant="h1" sx={{ maxWidth: 760 }}>
            Keep your trucks loaded.
          </Typography>
          <Typography sx={{ mt: 2.5, fontSize: '1.15rem', maxWidth: 560, color: '#C9CED2' }}>
            Join as a truck owner to receive verified bookings, or as a driver to run trips from your phone — OTP pickup,
            navigation-ready addresses and proof of delivery in the app.
          </Typography>
          <Stack direction="row" spacing={1.5} sx={{ mt: 4, flexWrap: 'wrap', gap: 1.5 }}>
            <Button component={RouterLink} to="/register?as=VehicleOwner" size="large" sx={{ bgcolor: palette.plate, color: palette.asphalt, '&:hover': { bgcolor: '#E3B610' } }}>
              Register my trucks
            </Button>
            <Button component={RouterLink} to="/register?as=Driver" size="large" variant="outlined" sx={{ color: '#fff', borderColor: 'rgba(255,255,255,.5)' }}>
              Register as a driver
            </Button>
          </Stack>
        </Container>
      </Box>
      <Container maxWidth="lg" sx={{ py: { xs: 6, md: 8 } }}>
        <Grid container spacing={4}>
          {points.map(([title, text]) => (
            <Grid key={title} size={{ xs: 12, md: 4 }}>
              <Typography variant="h4" component="h2">
                {title}
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 1 }}>
                {text}
              </Typography>
            </Grid>
          ))}
        </Grid>
        <Typography variant="h3" component="h2" sx={{ mt: 7 }}>
          What we check before your first trip
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 680 }}>
          Owner: PAN and bank account. Vehicle: RC, national or state permit, insurance, fitness certificate and PUC. Driver:
          a valid heavy or light goods vehicle licence. Upload them from your phone; our team usually verifies within one working day.
        </Typography>
      </Container>
    </>
  );
}

const contactSchema = z.object({
  fullName: v.personName,
  email: v.email,
  phoneNumber: v.optionalPhone,
  subject: v.safeText(200).pipe(z.string().min(3, 'Add a subject')),
  message: v.safeText(2000).pipe(z.string().min(10, 'Tell us a little more')),
  website: z.string().optional(),
});

export function ContactPage() {
  const [done, setDone] = useState('');
  const [error, setError] = useState('');
  const { control, handleSubmit, register, setError: setFieldError, formState } = useForm<z.input<typeof contactSchema>>({
    resolver: zodResolver(contactSchema),
    defaultValues: { fullName: '', email: '', phoneNumber: '', subject: '', message: '', website: '' },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      setDone((await referenceApi.contact(d)).message);
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Container maxWidth="lg" sx={{ py: { xs: 4, md: 8 } }}>
      <Grid container spacing={5}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Typography variant="h2" component="h1">
            Talk to the ProCargo desk
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 2 }}>
            For regular lanes, contract rates or anything about a booking. Existing customers get faster answers from Help &
            complaints after signing in.
          </Typography>
          <Typography sx={{ mt: 3, fontWeight: 650 }}>support@procargo.com</Typography>
          <Typography color="text.secondary">Monday to Saturday, 8 am to 8 pm IST</Typography>
        </Grid>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card>
            <CardContent sx={{ p: { xs: 2.5, md: 4 } }}>
              {done ? (
                <Alert severity="success">{done}</Alert>
              ) : (
                <Box component="form" onSubmit={submit} noValidate>
                  {error && (
                    <Alert severity="error" sx={{ mb: 2 }}>
                      {error}
                    </Alert>
                  )}
                  <Grid container spacing={2}>
                    <Grid size={{ xs: 12, sm: 6 }}>
                      <FormField control={control} name="fullName" label="Your name" autoComplete="name" />
                    </Grid>
                    <Grid size={{ xs: 12, sm: 6 }}>
                      <FormField control={control} name="phoneNumber" label="Mobile (optional)" type="tel" inputMode="tel" />
                    </Grid>
                    <Grid size={12}>
                      <FormField control={control} name="email" label="E-mail" type="email" autoComplete="email" />
                    </Grid>
                    <Grid size={12}>
                      <FormField control={control} name="subject" label="Subject" />
                    </Grid>
                    <Grid size={12}>
                      <FormField control={control} name="message" label="Message" multiline rows={5} />
                    </Grid>
                  </Grid>
                  {/* Honeypot: hidden from people, filled in by bots. */}
                  <Box sx={{ position: 'absolute', left: '-10000px' }} aria-hidden>
                    <input type="text" tabIndex={-1} autoComplete="off" {...register('website')} />
                  </Box>
                  <Button type="submit" variant="contained" size="large" sx={{ mt: 3 }} disabled={formState.isSubmitting}>
                    Send message
                  </Button>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Container>
  );
}

export function NotFoundPage() {
  return (
    <Container maxWidth="sm" sx={{ py: 10 }}>
      <Typography variant="h2" component="h1">
        This page doesn't exist
      </Typography>
      <Typography color="text.secondary" sx={{ mt: 1.5 }}>
        The link may be old, or the address mistyped.
      </Typography>
      <Button component={RouterLink} to="/" variant="contained" sx={{ mt: 3 }}>
        Go to the home page
      </Button>
    </Container>
  );
}
