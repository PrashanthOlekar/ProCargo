import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  MenuItem,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { authApi, notificationApi, supportApi } from '../../api/endpoints';
import type { ComplaintListItem, NotificationItem, TicketListItem } from '../../api/types';
import { toApiError } from '../../api/client';
import { homePathFor, useAuth } from '../../auth/AuthContext';
import { DataTable } from '../../components/DataTable';
import { applyApiErrors, FormField, useNotify } from '../../components/Forms';
import { EmptyState, PageHeader, QueryView, Section } from '../../components/Layout';
import { StatusChip } from '../../components/PlateTag';
import { formatDateTime, fromNow } from '../../lib/format';
import { complaintStatus, ticketPriority, ticketStatus } from '../../lib/statuses';
import * as v from '../../lib/validation';

// ---------------- password ----------------

const passwordSchema = z
  .object({ currentPassword: z.string().min(1, 'Enter your current password'), newPassword: v.password, confirmPassword: z.string() })
  .refine((d) => d.newPassword === d.confirmPassword, { path: ['confirmPassword'], message: 'The passwords do not match' })
  .refine((d) => d.newPassword !== d.currentPassword, { path: ['newPassword'], message: 'Choose a different password' });

export function ChangePasswordPage() {
  const { user, signOut } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof passwordSchema>>({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const submit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setError('');
    try {
      const result = await authApi.changePassword(currentPassword, newPassword);
      notify(result.message);
      await signOut();
      navigate('/login', { replace: true });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      {user && !user.mustChangePassword && (
        <Button component={RouterLink} to={homePathFor(user)} startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
          Back
        </Button>
      )}
      <Card>
        <CardContent sx={{ p: { xs: 3, md: 4 } }}>
          <Typography variant="h3" component="h1">
            {user?.mustChangePassword ? 'Set a new password to continue' : 'Change password'}
          </Typography>
          <Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>
            You will be signed out on every device and asked to sign in again.
          </Typography>
          <Box component="form" onSubmit={submit} noValidate>
            {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
            <Stack spacing={2}>
              <FormField control={control} name="currentPassword" label="Current password" type="password" autoComplete="current-password" />
              <FormField control={control} name="newPassword" label="New password" type="password" autoComplete="new-password" helperText="8+ characters with upper, lower, number and symbol" />
              <FormField control={control} name="confirmPassword" label="Repeat new password" type="password" autoComplete="new-password" />
            </Stack>
            <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 3 }} disabled={formState.isSubmitting}>
              Change password
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Container>
  );
}

// ---------------- notifications ----------------

const linkFor = (n: NotificationItem, role: string | null): string | null => {
  if (!n.entityType || !n.entityId) return null;
  switch (n.entityType) {
    case 'Booking':
      return role === 'customer' ? `/customer/bookings/${n.entityId}` : null;
    case 'Invoice':
      return role === 'customer' ? `/customer/invoices/${n.entityId}` : null;
    case 'Trip':
      return role === 'driver' ? `/driver/trips/${n.entityId}` : role === 'owner' ? `/owner/trips/${n.entityId}` : null;
    case 'Settlement':
      return role === 'owner' ? '/owner/earnings' : null;
    case 'SupportTicket':
      return `/support/tickets/${n.entityId}`;
    default:
      return null;
  }
};

export function NotificationsPage() {
  const { role } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: ['notifications', 'list'], queryFn: () => notificationApi.list({ pageSize: 50 }) });

  const open = async (n: NotificationItem) => {
    if (!n.readDateUtc) {
      await notificationApi.markRead(n.notificationId).catch(() => undefined);
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    }
    const to = linkFor(n, role);
    if (to) navigate(to);
  };

  return (
    <>
      <PageHeader
        title="Notifications"
        actions={
          <Button
            onClick={async () => {
              await notificationApi.markAllRead();
              queryClient.invalidateQueries({ queryKey: ['notifications'] });
            }}
          >
            Mark all as read
          </Button>
        }
      />
      <QueryView query={query}>
        {(page) =>
          page.items.length === 0 ? (
            <EmptyState title="You're all caught up">Quotations, trip updates and payment receipts will appear here.</EmptyState>
          ) : (
            <Stack spacing={1}>
              {page.items.map((n) => (
                <Card key={n.notificationId} sx={{ borderLeft: 4, borderLeftColor: n.readDateUtc ? 'divider' : 'primary.main' }}>
                  <CardActionArea onClick={() => open(n)}>
                    <CardContent>
                      <Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}>
                        <Typography sx={{ fontWeight: n.readDateUtc ? 500 : 750 }}>{n.title}</Typography>
                        <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
                          {fromNow(n.createdDateUtc)}
                        </Typography>
                      </Stack>
                      <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                        {n.message}
                      </Typography>
                    </CardContent>
                  </CardActionArea>
                </Card>
              ))}
            </Stack>
          )
        }
      </QueryView>
    </>
  );
}

// ---------------- support ----------------

const ticketSchema = z.object({
  subject: v.safeText(200).pipe(z.string().min(5, 'Add a short subject')),
  description: v.safeText(2000).pipe(z.string().min(10, 'Tell us what happened')),
  priority: z.enum(['Low', 'Medium', 'High', 'Critical']),
});

const complaintSchema = z.object({
  category: z.enum(['Delay', 'Damage', 'Behaviour', 'Billing', 'Payment', 'Other']),
  subject: v.safeText(200).pipe(z.string().min(5, 'Add a short subject')),
  description: v.safeText(2000).pipe(z.string().min(10, 'Describe the complaint')),
});

export function SupportPage() {
  const [tab, setTab] = useState(0);
  const [newTicket, setNewTicket] = useState(false);
  const [newComplaint, setNewComplaint] = useState(false);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return (
    <>
      <PageHeader
        title="Help & complaints"
        subtitle="Questions go to a support ticket. Something went wrong with a delivery or a bill? Raise a complaint and we investigate it formally."
        actions={
          <>
            <Button variant="contained" onClick={() => setNewTicket(true)}>
              Ask for help
            </Button>
            <Button variant="outlined" onClick={() => setNewComplaint(true)}>
              Raise a complaint
            </Button>
          </>
        }
      />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} sx={{ mb: 2 }}>
        <Tab label="Support tickets" />
        <Tab label="Complaints" />
      </Tabs>
      {tab === 0 ? (
        <DataTable<TicketListItem>
          queryKey={['tickets']}
          fetcher={(q) => supportApi.tickets(q)}
          rowKey={(t) => t.supportTicketId}
          onRowClick={(t) => navigate(`/support/tickets/${t.supportTicketId}`)}
          empty={{ title: 'No support tickets', text: 'Ask us anything about a booking, a trip or your account.' }}
          columns={[
            { header: 'Ticket', primary: true, render: (t) => <Typography sx={{ fontWeight: 650 }}>{t.ticketNumber}</Typography> },
            { header: 'Subject', primary: true, render: (t) => t.subject },
            { header: 'Raised', render: (t) => formatDateTime(t.createdDateUtc) },
            { header: 'Status', primary: true, render: (t) => <StatusChip status={ticketStatus[t.ticketStatusId]} /> },
          ]}
        />
      ) : (
        <DataTable<ComplaintListItem>
          queryKey={['complaints']}
          fetcher={(q) => supportApi.complaints(q)}
          rowKey={(c) => c.complaintId}
          onRowClick={(c) => navigate(`/support/complaints/${c.complaintId}`)}
          empty={{ title: 'No complaints', text: 'We hope it stays that way.' }}
          columns={[
            { header: 'Complaint', primary: true, render: (c) => <Typography sx={{ fontWeight: 650 }}>{c.complaintNumber}</Typography> },
            { header: 'Category', render: (c) => c.category },
            { header: 'Subject', primary: true, render: (c) => c.subject },
            { header: 'Raised', render: (c) => formatDateTime(c.createdDateUtc) },
            { header: 'Status', primary: true, render: (c) => <StatusChip status={complaintStatus[c.complaintStatusId]} /> },
          ]}
        />
      )}

      <CreateDialog
        open={newTicket}
        title="Ask for help"
        schema={ticketSchema}
        defaults={{ subject: '', description: '', priority: 'Medium' }}
        onClose={() => setNewTicket(false)}
        submit={(d) => supportApi.createTicket(d)}
        onCreated={(id) => {
          queryClient.invalidateQueries({ queryKey: ['tickets'] });
          navigate(`/support/tickets/${id}`);
        }}
        fields={(control) => (
          <>
            <FormField control={control} name="subject" label="Subject" />
            <FormField control={control} name="description" label="How can we help?" multiline rows={5} />
            <FormField control={control} name="priority" label="How urgent is it?" select>
              <MenuItem value="Low">It can wait</MenuItem>
              <MenuItem value="Medium">Today, please</MenuItem>
              <MenuItem value="High">A trip is affected now</MenuItem>
              <MenuItem value="Critical">Goods or people are at risk</MenuItem>
            </FormField>
          </>
        )}
      />
      <CreateDialog
        open={newComplaint}
        title="Raise a complaint"
        schema={complaintSchema}
        defaults={{ category: 'Delay', subject: '', description: '' }}
        onClose={() => setNewComplaint(false)}
        submit={(d) => supportApi.createComplaint(d)}
        onCreated={(id) => {
          queryClient.invalidateQueries({ queryKey: ['complaints'] });
          setTab(1);
          navigate(`/support/complaints/${id}`);
        }}
        fields={(control) => (
          <>
            <FormField control={control} name="category" label="What is it about?" select>
              <MenuItem value="Delay">Late pickup or delivery</MenuItem>
              <MenuItem value="Damage">Damaged or missing goods</MenuItem>
              <MenuItem value="Behaviour">Driver or staff behaviour</MenuItem>
              <MenuItem value="Billing">Invoice or charges</MenuItem>
              <MenuItem value="Payment">Payment or payout</MenuItem>
              <MenuItem value="Other">Something else</MenuItem>
            </FormField>
            <FormField control={control} name="subject" label="Subject" helperText="Include the booking or trip number if there is one" />
            <FormField control={control} name="description" label="What happened?" multiline rows={5} />
          </>
        )}
      />
    </>
  );
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function CreateDialog<S extends z.ZodType<any, any, any>>({
  open,
  title,
  schema,
  defaults,
  fields,
  submit,
  onClose,
  onCreated,
}: {
  open: boolean;
  title: string;
  schema: S;
  defaults: z.infer<S>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  fields: (control: any) => React.ReactNode;
  submit: (data: z.infer<S>) => Promise<{ id: number }>;
  onClose: () => void;
  onCreated: (id: number) => void;
}) {
  const notify = useNotify();
  const [error, setError] = useState('');
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const { control, handleSubmit, reset, setError: setFieldError, formState } = useForm<any>({ resolver: zodResolver(schema as any), defaultValues: defaults });

  const close = () => {
    reset(defaults);
    setError('');
    onClose();
  };

  const onSubmit = handleSubmit(async (d) => {
    setError('');
    try {
      const created = await submit(d as z.infer<S>);
      notify('Sent. We usually reply within a few hours.');
      close();
      onCreated(created.id);
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open={open} onClose={close} maxWidth="sm" fullWidth>
      <form onSubmit={onSubmit} noValidate>
        <DialogTitle>{title}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Stack spacing={2} sx={{ mt: 1 }}>
            {fields(control)}
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={close}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            Send
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

export function TicketDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['ticket', id], queryFn: () => supportApi.ticket(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [reply, setReply] = useState('');
  const [busy, setBusy] = useState(false);

  return (
    <QueryView query={query}>
      {({ ticket: t, comments }) => (
        <>
          <PageHeader
            back={
              <Button component={RouterLink} to="/support" startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
                Help & complaints
              </Button>
            }
            title={t.subject}
            subtitle={
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <span>{t.ticketNumber}</span>
                <StatusChip status={ticketStatus[t.ticketStatusId]} />
                <span>{ticketPriority[t.ticketPriorityId]} priority</span>
              </Stack>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 8 }}>
              <Section>
                <Typography variant="body2" color="text.secondary">
                  You wrote, {formatDateTime(t.createdDateUtc)}
                </Typography>
                <Typography sx={{ mt: 1, whiteSpace: 'pre-wrap' }}>{t.description}</Typography>
              </Section>
              {comments.map((c) => (
                <Box
                  key={c.supportTicketCommentId}
                  sx={{ mb: 1.5, ml: c.isStaff ? 0 : { sm: 6 }, mr: c.isStaff ? { sm: 6 } : 0, p: 2, borderRadius: 1.5, bgcolor: c.isStaff ? 'background.paper' : '#E3EFEE', border: 1, borderColor: 'divider' }}
                >
                  <Typography variant="body2" color="text.secondary">
                    {c.isStaff ? `${c.createdByName}, ProCargo support` : 'You'} — {formatDateTime(c.createdDateUtc)}
                  </Typography>
                  <Typography sx={{ mt: 0.5, whiteSpace: 'pre-wrap' }}>{c.commentText}</Typography>
                </Box>
              ))}
              {t.ticketStatusId !== 5 ? (
                <Section>
                  <TextField label="Reply" value={reply} onChange={(e) => setReply(e.target.value)} multiline rows={3} />
                  <Button
                    variant="contained"
                    sx={{ mt: 1.5 }}
                    disabled={busy || reply.trim().length < 2}
                    onClick={async () => {
                      setBusy(true);
                      try {
                        await supportApi.comment(id, reply.trim());
                        setReply('');
                        queryClient.invalidateQueries({ queryKey: ['ticket', id] });
                      } catch (e) {
                        notify(toApiError(e).message, 'error');
                      } finally {
                        setBusy(false);
                      }
                    }}
                  >
                    Send reply
                  </Button>
                </Section>
              ) : (
                <Alert severity="info">This ticket is closed. Raise a new one if you need more help.</Alert>
              )}
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

export function ComplaintDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['complaint', id], queryFn: () => supportApi.complaint(id) });
  return (
    <QueryView query={query}>
      {({ complaint: c }) => (
        <>
          <PageHeader
            back={
              <Button component={RouterLink} to="/support" startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
                Help & complaints
              </Button>
            }
            title={c.subject}
            subtitle={
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <span>
                  {c.complaintNumber}, {c.category}
                </span>
                <StatusChip status={complaintStatus[c.complaintStatusId]} />
              </Stack>
            }
          />
          <Section>
            <Typography variant="body2" color="text.secondary">
              Raised {formatDateTime(c.createdDateUtc)}
            </Typography>
            <Typography sx={{ mt: 1, whiteSpace: 'pre-wrap' }}>{c.description}</Typography>
          </Section>
          {c.resolution && (
            <Section title="Our response">
              <Typography sx={{ whiteSpace: 'pre-wrap' }}>{c.resolution}</Typography>
              {c.resolvedDateUtc && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                  {formatDateTime(c.resolvedDateUtc)}
                </Typography>
              )}
            </Section>
          )}
        </>
      )}
    </QueryView>
  );
}
