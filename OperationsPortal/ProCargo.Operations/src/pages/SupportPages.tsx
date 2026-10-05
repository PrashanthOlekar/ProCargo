import { useState } from 'react';
import { useNavigate, useParams, Link as RouterLink } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  Grid,
  Link,
  MenuItem,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { supportApi } from '../api/endpoints';
import type { ComplaintListItem, ContactEnquiry, TicketListItem } from '../api/types';
import { toApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { DataTable } from '../components/DataTable';
import { FilterSelect, statusOptions, useUrlFilter } from '../components/Filters';
import { ReasonDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { formatDateTime, fromNow } from '../lib/format';
import { complaintStatus, ticketPriority, ticketStatus } from '../lib/statuses';
import { back } from './BookingPages';

/** Mirrors StatusRules.Ticket on the API so the select only offers moves the server will accept. */
const ticketMoves: Record<number, number[]> = { 1: [2, 3, 4, 5], 2: [3, 4, 5], 3: [2, 4, 5], 4: [5, 2], 5: [] };
const priorityOptions = Object.entries(ticketPriority).map(([value, label]) => ({ value, label }));

// ---------------- tickets ----------------

export function TicketsPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  const [priority, setPriority] = useUrlFilter('priority');
  const [mine, setMine] = useUrlFilter('mine');
  return (
    <>
      <PageHeader title="Support tickets" subtitle="Questions and problems raised by customers, owners and drivers." />
      <DataTable<TicketListItem>
        queryKey={['tickets', status, priority, mine]}
        fetcher={(q) =>
          supportApi.tickets({ ...q, status: status ? Number(status) : undefined, priority: priority ? Number(priority) : undefined, assignedToMe: mine === '1' || undefined })
        }
        rowKey={(t) => t.supportTicketId}
        onRowClick={(t) => navigate(`/support/tickets/${t.supportTicketId}`)}
        searchPlaceholder="Ticket number or subject"
        filters={
          <>
            <FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(ticketStatus)} />
            <FilterSelect label="Priority" value={priority} onChange={setPriority} options={priorityOptions} width={160} />
            <FormControlLabel control={<Checkbox checked={mine === '1'} onChange={(e) => setMine(e.target.checked ? '1' : '')} />} label="Assigned to me" />
          </>
        }
        empty={{ title: 'No tickets match', text: 'Clear the filters to see every ticket.' }}
        columns={[
          { header: 'Ticket', primary: true, render: (t) => <Typography sx={{ fontWeight: 650 }}>{t.ticketNumber}</Typography> },
          { header: 'Subject', primary: true, render: (t) => t.subject },
          { header: 'Raised by', render: (t) => t.raisedByName },
          { header: 'Priority', render: (t) => <PriorityText id={t.ticketPriorityId} /> },
          { header: 'Assigned to', render: (t) => t.assignedToName ?? <Typography color="text.secondary" variant="body2">Nobody</Typography> },
          { header: 'Opened', render: (t) => fromNow(t.createdDateUtc) },
          { header: 'Status', primary: true, render: (t) => <StatusChip status={ticketStatus[t.ticketStatusId]} /> },
        ]}
      />
    </>
  );
}

function PriorityText({ id }: { id: number }) {
  return (
    <Typography variant="body2" sx={{ fontWeight: id >= 3 ? 700 : 400, color: id === 4 ? 'error.main' : id === 3 ? 'warning.dark' : 'text.primary' }}>
      {ticketPriority[id]}
    </Typography>
  );
}

export function TicketDetailPage() {
  const id = Number(useParams().id);
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['ticket', id], queryFn: () => supportApi.ticket(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['ticket', id] });
    queryClient.invalidateQueries({ queryKey: ['tickets'] });
  };

  const [reply, setReply] = useState('');
  const [internal, setInternal] = useState(false);
  const comment = useMutation({
    mutationFn: () => supportApi.comment(id, reply.trim(), internal),
    onSuccess: () => {
      setReply('');
      notify(internal ? 'Internal note added' : 'Reply sent');
      setInternal(false);
      refresh();
    },
    onError: (e) => notify(toApiError(e).message, 'error'),
  });

  const update = useMutation({
    mutationFn: (body: { status: number; priority: number; assignedToUserId: number | null }) => supportApi.updateTicket(id, body),
    onSuccess: () => {
      notify('Ticket updated');
      refresh();
    },
    onError: (e) => notify(toApiError(e).message, 'error'),
  });

  return (
    <QueryView query={query}>
      {({ ticket: t, comments }) => {
        const current = { status: t.ticketStatusId, priority: t.ticketPriorityId, assignedToUserId: t.assignedToUserId };
        const closed = t.ticketStatusId === 5;
        return (
          <>
            <PageHeader back={back('/support/tickets', 'Tickets')} title={`${t.ticketNumber}: ${t.subject}`} subtitle={<StatusChip status={ticketStatus[t.ticketStatusId]} size="medium" />} />
            <Grid container spacing={3}>
              <Grid size={{ xs: 12, md: 8 }}>
                <Section title="Conversation">
                  <Stack spacing={2}>
                    <Message author={t.raisedByName} when={t.createdDateUtc} text={t.description} />
                    {comments.map((c) => (
                      <Message key={c.supportTicketCommentId} author={c.createdByName} when={c.createdDateUtc} text={c.commentText} staff={c.isStaff} internal={c.isInternal} />
                    ))}
                  </Stack>
                  {closed ? (
                    <Alert severity="info" sx={{ mt: 3 }}>This ticket is closed. The person can raise a new ticket if they need more help.</Alert>
                  ) : (
                    <Box component="form" sx={{ mt: 3 }} onSubmit={(e) => { e.preventDefault(); if (reply.trim().length >= 2) comment.mutate(); }}>
                      <TextField
                        label={internal ? 'Internal note (staff only)' : `Reply to ${t.raisedByName}`}
                        value={reply}
                        onChange={(e) => setReply(e.target.value)}
                        multiline
                        minRows={3}
                        fullWidth
                        slotProps={{ htmlInput: { maxLength: 4000 } }}
                      />
                      <Stack direction="row" sx={{ mt: 1, justifyContent: 'space-between', alignItems: 'center' }}>
                        <FormControlLabel control={<Checkbox checked={internal} onChange={(e) => setInternal(e.target.checked)} />} label="Internal note" />
                        <Button type="submit" variant="contained" disabled={reply.trim().length < 2 || comment.isPending}>
                          {internal ? 'Add note' : 'Send reply'}
                        </Button>
                      </Stack>
                    </Box>
                  )}
                </Section>
              </Grid>
              <Grid size={{ xs: 12, md: 4 }}>
                <Section title="Handling">
                  <Stack spacing={2}>
                    <TextField
                      select
                      label="Status"
                      value={t.ticketStatusId}
                      disabled={closed || update.isPending}
                      onChange={(e) => update.mutate({ ...current, status: Number(e.target.value) })}
                    >
                      {[t.ticketStatusId, ...ticketMoves[t.ticketStatusId]].map((s) => (
                        <MenuItem key={s} value={s}>{ticketStatus[s].label}</MenuItem>
                      ))}
                    </TextField>
                    <TextField
                      select
                      label="Priority"
                      value={t.ticketPriorityId}
                      disabled={closed || update.isPending}
                      onChange={(e) => update.mutate({ ...current, priority: Number(e.target.value) })}
                    >
                      {priorityOptions.map((p) => <MenuItem key={p.value} value={Number(p.value)}>{p.label}</MenuItem>)}
                    </TextField>
                    <Box>
                      <Typography variant="body2" color="text.secondary">Assigned to</Typography>
                      <Typography sx={{ mb: 1 }}>{t.assignedToName ?? 'Nobody yet'}</Typography>
                      <Stack direction="row" spacing={1}>
                        {t.assignedToUserId !== user?.userId && (
                          <Button size="small" variant="outlined" disabled={closed || update.isPending} onClick={() => update.mutate({ ...current, assignedToUserId: user!.userId })}>
                            Assign to me
                          </Button>
                        )}
                        {t.assignedToUserId && (
                          <Button size="small" disabled={closed || update.isPending} onClick={() => update.mutate({ ...current, assignedToUserId: null })}>
                            Unassign
                          </Button>
                        )}
                      </Stack>
                    </Box>
                  </Stack>
                </Section>
                <Section title="Details">
                  <DetailList
                    columns={1}
                    items={[
                      ['Raised by', t.raisedByName],
                      ['Opened', formatDateTime(t.createdDateUtc)],
                      ['Booking', t.bookingNumber && t.bookingId ? <Link component={RouterLink} to={`/bookings/${t.bookingId}`}><PlateTag size="small">{t.bookingNumber}</PlateTag></Link> : '—'],
                      ['Closed', formatDateTime(t.closedDateUtc)],
                    ]}
                  />
                </Section>
              </Grid>
            </Grid>
          </>
        );
      }}
    </QueryView>
  );
}

function Message({ author, when, text, staff, internal }: { author: string; when: string; text: string; staff?: boolean; internal?: boolean }) {
  return (
    <Box
      sx={{
        p: 2,
        borderRadius: 1.5,
        border: 1,
        borderColor: internal ? 'warning.light' : 'divider',
        bgcolor: internal ? 'rgba(245,197,24,0.08)' : staff ? 'rgba(15,95,91,0.05)' : 'background.paper',
        ml: staff ? { sm: 6 } : 0,
        mr: staff ? 0 : { sm: 6 },
      }}
    >
      <Stack direction="row" sx={{ justifyContent: 'space-between', mb: 0.5, gap: 1, flexWrap: 'wrap' }}>
        <Typography variant="body2" sx={{ fontWeight: 650 }}>
          {author}
          {internal ? ' (internal note)' : staff ? ' (ProCargo)' : ''}
        </Typography>
        <Typography variant="body2" color="text.secondary">{formatDateTime(when)}</Typography>
      </Stack>
      <Typography sx={{ whiteSpace: 'pre-wrap' }}>{text}</Typography>
    </Box>
  );
}

// ---------------- complaints ----------------

const complaintCategories = ['Delay', 'Damage', 'Behaviour', 'Billing', 'Payment', 'Other'].map((c) => ({ value: c, label: c }));

export function ComplaintsPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  const [category, setCategory] = useUrlFilter('category');
  const [mine, setMine] = useUrlFilter('mine');
  return (
    <>
      <PageHeader title="Complaints" subtitle="Formal complaints about a delivery, a driver, billing or payment." />
      <DataTable<ComplaintListItem>
        queryKey={['complaints', status, category, mine]}
        fetcher={(q) => supportApi.complaints({ ...q, status: status ? Number(status) : undefined, category: category || undefined, assignedToMe: mine === '1' || undefined })}
        rowKey={(c) => c.complaintId}
        onRowClick={(c) => navigate(`/support/complaints/${c.complaintId}`)}
        searchPlaceholder="Complaint number or subject"
        filters={
          <>
            <FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(complaintStatus)} />
            <FilterSelect label="Category" value={category} onChange={setCategory} options={complaintCategories} width={160} />
            <FormControlLabel control={<Checkbox checked={mine === '1'} onChange={(e) => setMine(e.target.checked ? '1' : '')} />} label="Assigned to me" />
          </>
        }
        empty={{ title: 'No complaints match' }}
        columns={[
          { header: 'Complaint', primary: true, render: (c) => <Typography sx={{ fontWeight: 650 }}>{c.complaintNumber}</Typography> },
          { header: 'Category', render: (c) => c.category },
          { header: 'Subject', primary: true, render: (c) => c.subject },
          { header: 'Raised by', render: (c) => c.raisedByName },
          { header: 'Assigned to', render: (c) => c.assignedToName ?? '—' },
          { header: 'Opened', render: (c) => fromNow(c.createdDateUtc) },
          { header: 'Status', primary: true, render: (c) => <StatusChip status={complaintStatus[c.complaintStatusId]} /> },
        ]}
      />
    </>
  );
}

export function ComplaintDetailPage() {
  const id = Number(useParams().id);
  const { user } = useAuth();
  const query = useQuery({ queryKey: ['complaint', id], queryFn: () => supportApi.complaint(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [closing, setClosing] = useState<'Resolved' | 'Rejected' | null>(null);
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['complaint', id] });
    queryClient.invalidateQueries({ queryKey: ['complaints'] });
  };
  const run = async (fn: () => Promise<unknown>, done: string) => {
    try {
      await fn();
      notify(done);
      refresh();
    } catch (e) {
      notify(toApiError(e).message, 'error');
    }
  };

  return (
    <QueryView query={query}>
      {({ complaint: c, availableActions: a }) => (
        <>
          <PageHeader
            back={back('/support/complaints', 'Complaints')}
            title={`${c.complaintNumber}: ${c.subject}`}
            subtitle={<StatusChip status={complaintStatus[c.complaintStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('Assign') && c.assignedToUserId !== user?.userId && (
                  <Button variant="outlined" onClick={() => run(() => supportApi.assignComplaint(id, user!.userId), 'Assigned to you')}>Assign to me</Button>
                )}
                {a.includes('Investigating') && (
                  <Button variant="outlined" onClick={() => run(() => supportApi.complaintStatus(id, 'Investigating'), 'Marked as investigating')}>
                    {c.complaintStatusId === 4 ? 'Reopen' : 'Start investigating'}
                  </Button>
                )}
                {a.includes('Rejected') && <Button color="error" onClick={() => setClosing('Rejected')}>Reject</Button>}
                {a.includes('Resolved') && <Button variant="contained" onClick={() => setClosing('Resolved')}>Resolve</Button>}
                {a.includes('Closed') && <Button variant="contained" onClick={() => run(() => supportApi.complaintStatus(id, 'Closed'), 'Complaint closed')}>Close</Button>}
              </>
            }
          />
          <Grid container spacing={3}>
            <Grid size={{ xs: 12, md: 8 }}>
              <Section title="What happened">
                <Typography sx={{ whiteSpace: 'pre-wrap' }}>{c.description}</Typography>
              </Section>
              {c.resolution && (
                <Section title={c.complaintStatusId === 5 ? 'Why it was rejected' : 'Resolution'}>
                  <Typography sx={{ whiteSpace: 'pre-wrap' }}>{c.resolution}</Typography>
                </Section>
              )}
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Section title="Details">
                <DetailList
                  columns={1}
                  items={[
                    ['Category', c.category],
                    ['Raised by', c.raisedByName],
                    ['Opened', formatDateTime(c.createdDateUtc)],
                    ['Assigned to', c.assignedToName ?? 'Nobody yet'],
                    ['Booking', c.bookingNumber ? <PlateTag size="small">{c.bookingNumber}</PlateTag> : '—'],
                    ['Trip', c.tripNumber ? <PlateTag size="small">{c.tripNumber}</PlateTag> : '—'],
                    ['Resolved', formatDateTime(c.resolvedDateUtc)],
                  ]}
                />
              </Section>
            </Grid>
          </Grid>
          <ReasonDialog
            open={closing !== null}
            title={closing === 'Rejected' ? 'Reject complaint' : 'Resolve complaint'}
            description="The person who raised the complaint is notified and can read this."
            label={closing === 'Rejected' ? 'Why it is being rejected' : 'How it was resolved'}
            confirmLabel={closing === 'Rejected' ? 'Reject' : 'Resolve'}
            destructive={closing === 'Rejected'}
            onClose={() => setClosing(null)}
            onSubmit={async (text) => {
              await supportApi.complaintStatus(id, closing!, text);
              notify(closing === 'Rejected' ? 'Complaint rejected' : 'Complaint resolved');
              refresh();
            }}
          />
        </>
      )}
    </QueryView>
  );
}

// ---------------- contact enquiries ----------------

export function EnquiriesPage() {
  const [tab, setTab] = useState(0);
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState<number | null>(null);
  const handled = tab === 1;

  const markHandled = async (e: ContactEnquiry) => {
    try {
      await supportApi.enquiryHandled(e.contactEnquiryId);
      notify('Marked as handled');
      queryClient.invalidateQueries({ queryKey: ['enquiries'] });
    } catch (err) {
      notify(toApiError(err).message, 'error');
    }
  };

  return (
    <>
      <PageHeader title="Website enquiries" subtitle="Messages sent through the public contact form. Reply by e-mail or phone, then mark them handled." />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} sx={{ mb: 2 }}>
        <Tab label="Waiting" />
        <Tab label="Handled" />
      </Tabs>
      <DataTable<ContactEnquiry>
        queryKey={['enquiries', handled]}
        fetcher={(q) => supportApi.enquiries({ ...q, isHandled: handled })}
        rowKey={(e) => e.contactEnquiryId}
        onRowClick={(e) => setOpen(open === e.contactEnquiryId ? null : e.contactEnquiryId)}
        searchPlaceholder="Name, e-mail or subject"
        empty={{ title: handled ? 'Nothing handled yet' : 'No enquiries waiting' }}
        columns={[
          { header: 'Received', render: (e) => formatDateTime(e.createdDateUtc) },
          {
            header: 'From',
            primary: true,
            render: (e) => (
              <>
                <Typography sx={{ fontWeight: 600 }}>{e.fullName}</Typography>
                <Typography variant="body2">
                  <Link href={`mailto:${e.email}`} onClick={(ev) => ev.stopPropagation()}>{e.email}</Link>
                  {e.phoneNumber ? `, ${e.phoneNumber}` : ''}
                </Typography>
              </>
            ),
          },
          {
            header: 'Message',
            primary: true,
            render: (e) => (
              <>
                <Typography sx={{ fontWeight: 600 }}>{e.subject}</Typography>
                <Typography variant="body2" color="text.secondary" sx={open === e.contactEnquiryId ? { whiteSpace: 'pre-wrap' } : { display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                  {e.message}
                </Typography>
              </>
            ),
          },
          {
            header: '',
            align: 'right',
            render: (e) =>
              e.isHandled ? (
                <Typography variant="body2" color="text.secondary">{formatDateTime(e.handledDateUtc)}</Typography>
              ) : (
                <Button size="small" variant="outlined" onClick={(ev) => { ev.stopPropagation(); markHandled(e); }}>Mark handled</Button>
              ),
          },
        ]}
      />
    </>
  );
}
