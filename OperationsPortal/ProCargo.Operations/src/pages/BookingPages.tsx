import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControlLabel,
  Grid,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bookingApi, financeApi, quotationApi, tripApi } from '../api/endpoints';
import type { BookingDetails, BookingListItem, PriceEstimate, QuotationListItem } from '../api/types';
import { toApiError } from '../api/client';
import { P, useAuth } from '../auth/AuthContext';
import { DataTable } from '../components/DataTable';
import { FilterSelect, statusOptions, useDialog, useUrlFilter } from '../components/Filters';
import { ReasonDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { StatusTimeline } from '../components/Timeline';
import { formatDate, formatDateTime, formatKg, formatKm, formatMoney, fromLocalInput, toLocalInput } from '../lib/format';
import { bookingStatus, quotationStatus } from '../lib/statuses';

export const back = (to: string, label: string) => (
  <Button component={RouterLink} to={to} startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
    {label}
  </Button>
);

export function BookingsPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  return (
    <>
      <PageHeader title="Bookings" />
      <DataTable<BookingListItem>
        queryKey={['bookings', status]}
        fetcher={(q) => bookingApi.list({ ...q, status: status ? Number(status) : undefined })}
        rowKey={(b) => b.bookingId}
        onRowClick={(b) => navigate(`/bookings/${b.bookingId}`)}
        searchPlaceholder="Booking number or customer"
        filters={<FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(bookingStatus)} />}
        empty={{ title: 'No bookings match' }}
        columns={[
          { header: 'Booking', primary: true, render: (b) => <PlateTag size="small">{b.bookingNumber}</PlateTag> },
          { header: 'Customer', primary: true, render: (b) => b.customerName },
          { header: 'Route', primary: true, render: (b) => `${b.pickupCityName} → ${b.deliveryCityName}` },
          { header: 'Vehicle', render: (b) => b.vehicleTypeName },
          { header: 'Load', align: 'right', render: (b) => formatKg(b.totalWeightKg) },
          { header: 'Pickup', render: (b) => formatDateTime(b.requestedPickupDateUtc) },
          { header: 'Status', primary: true, render: (b) => <StatusChip status={bookingStatus[b.bookingStatusId]} /> },
        ]}
      />
    </>
  );
}

type BookingDialog = 'quote' | 'assign' | 'hold' | 'reject' | 'cancel' | 'invoice';

export function BookingDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['booking', id], queryFn: () => bookingApi.get(id) });
  const history = useQuery({ queryKey: ['booking', id, 'history'], queryFn: () => bookingApi.history(id) });
  const quotations = useQuery({ queryKey: ['booking', id, 'quotations'], queryFn: () => quotationApi.list({ bookingId: id, pageSize: 20 }) });
  const notes = useQuery({ queryKey: ['booking', id, 'notes'], queryFn: () => bookingApi.notes(id) });
  const dialog = useDialog<BookingDialog>();
  const queryClient = useQueryClient();
  const notify = useNotify();
  const navigate = useNavigate();
  const [note, setNote] = useState('');
  const [internal, setInternal] = useState(true);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['booking', id] });
  const run = async (fn: () => Promise<unknown>, message: string) => {
    try {
      await fn();
      notify(message);
      refresh();
    } catch (e) {
      notify(toApiError(e).message, 'error');
    }
  };

  return (
    <QueryView query={query}>
      {({ booking: b, items, availableActions: a }) => (
        <>
          <PageHeader
            back={back('/bookings', 'Bookings')}
            title={
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                <PlateTag size="large">{b.bookingNumber}</PlateTag>
                <span>
                  {b.pickupCityName} → {b.deliveryCityName}
                </span>
              </Stack>
            }
            subtitle={<StatusChip status={bookingStatus[b.bookingStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('Review') && <Button onClick={() => run(() => bookingApi.action(id, 'review'), 'Marked as under review')}>Start review</Button>}
                {a.includes('CreateQuotation') && <Button variant="contained" onClick={() => dialog.show('quote')}>Prepare quotation</Button>}
                {a.includes('AssignTrip') && <Button variant="contained" onClick={() => dialog.show('assign')}>Assign truck</Button>}
                {a.includes('GenerateInvoice') && <Button variant="contained" onClick={() => dialog.show('invoice')}>Issue invoice</Button>}
                {a.includes('Hold') && <Button onClick={() => dialog.show('hold')}>Hold</Button>}
                {a.includes('Resume') && <Button onClick={() => run(() => bookingApi.action(id, 'resume'), 'Booking resumed')}>Resume</Button>}
                {a.includes('Reject') && <Button color="error" onClick={() => dialog.show('reject')}>Reject</Button>}
                {a.includes('Cancel') && <Button color="error" onClick={() => dialog.show('cancel')}>Cancel</Button>}
              </>
            }
          />
          {b.cancellationReason && <Alert severity="info" sx={{ mb: 2 }}>Reason: {b.cancellationReason}</Alert>}

          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, lg: 8 }}>
              <Section title="Customer and load">
                <DetailList
                  columns={3}
                  items={[
                    ['Customer', `${b.customerName}${b.customerCompanyName ? `, ${b.customerCompanyName}` : ''}`],
                    ['Customer mobile', b.customerPhoneNumber],
                    ['Vehicle', b.vehicleTypeName],
                    ['Goods', `${b.goodsTypeName}${b.requiresSpecialHandling ? ' (special handling)' : ''}`],
                    ['Description', b.goodsDescription],
                    ['Load', `${formatKg(b.totalWeightKg)}, ${b.totalQuantity} pcs`],
                    ['Pickup requested', formatDateTime(b.requestedPickupDateUtc)],
                    ['Estimated distance', formatKm(b.estimatedDistanceKm)],
                    ['Instructions', b.specialInstructions],
                  ]}
                />
                <Table size="small" sx={{ mt: 2 }}>
                  <TableBody>
                    {items.map((i) => (
                      <TableRow key={i.bookingItemId}>
                        <TableCell sx={{ pl: 0 }}>{i.description}{i.isFragile ? ' (fragile)' : ''}</TableCell>
                        <TableCell align="right">{i.quantity} pcs</TableCell>
                        <TableCell align="right" sx={{ pr: 0 }}>{formatKg(i.weightKg)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Section>
              <Section title="Route">
                <Grid container spacing={3}>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant="body2" color="text.secondary">Pickup</Typography>
                    <Typography sx={{ fontWeight: 600 }}>{b.pickupAddressLine1}</Typography>
                    <Typography>{[b.pickupAddressLine2, b.pickupLandmark && `Near ${b.pickupLandmark}`].filter(Boolean).join(', ')}</Typography>
                    <Typography>{b.pickupCityName} {b.pickupPincode}</Typography>
                    <Typography variant="body2" sx={{ mt: 1 }}>{b.pickupContactName}, {b.pickupContactPhone}</Typography>
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant="body2" color="text.secondary">Delivery</Typography>
                    <Typography sx={{ fontWeight: 600 }}>{b.deliveryAddressLine1}</Typography>
                    <Typography>{[b.deliveryAddressLine2, b.deliveryLandmark && `Near ${b.deliveryLandmark}`].filter(Boolean).join(', ')}</Typography>
                    <Typography>{b.deliveryCityName} {b.deliveryPincode}</Typography>
                    <Typography variant="body2" sx={{ mt: 1 }}>{b.deliveryContactName}, {b.deliveryContactPhone}</Typography>
                  </Grid>
                </Grid>
              </Section>
              <Section title="Notes">
                <Stack spacing={1.25} sx={{ mb: 2 }}>
                  {(notes.data ?? []).map((n) => (
                    <Box key={n.bookingNoteId} sx={{ borderLeft: 3, borderColor: n.isInternal ? 'warning.main' : 'primary.main', pl: 1.5 }}>
                      <Typography variant="body2" color="text.secondary">
                        {n.createdByName ?? 'System'}, {formatDateTime(n.createdDateUtc)}{n.isInternal ? ' (internal)' : ' (visible to customer)'}
                      </Typography>
                      <Typography sx={{ whiteSpace: 'pre-wrap' }}>{n.note}</Typography>
                    </Box>
                  ))}
                </Stack>
                <TextField label="Add a note" value={note} onChange={(e) => setNote(e.target.value)} multiline rows={2} />
                <Stack direction="row" sx={{ justifyContent: 'space-between', mt: 1 }}>
                  <FormControlLabel control={<Checkbox checked={internal} onChange={(e) => setInternal(e.target.checked)} />} label="Internal only" />
                  <Button
                    disabled={note.trim().length < 2}
                    onClick={() =>
                      run(async () => {
                        await bookingApi.addNote(id, note.trim(), internal);
                        setNote('');
                        queryClient.invalidateQueries({ queryKey: ['booking', id, 'notes'] });
                      }, 'Note added')
                    }
                  >
                    Add note
                  </Button>
                </Stack>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, lg: 4 }}>
              {b.tripId && (
                <Section title="Trip" action={<Button component={RouterLink} to={`/trips/${b.tripId}`}>Open trip</Button>}>
                  <DetailList
                    columns={1}
                    items={[
                      ['Trip', b.tripNumber],
                      ['Vehicle', b.assignedVehicleNumber ? <PlateTag size="small">{b.assignedVehicleNumber}</PlateTag> : '—'],
                      ['Driver', b.assignedDriverName ? `${b.assignedDriverName}, ${b.assignedDriverPhone}` : '—'],
                    ]}
                  />
                </Section>
              )}
              {b.invoiceId && (
                <Section title="Invoice" action={<Button component={RouterLink} to={`/invoices/${b.invoiceId}`}>Open invoice</Button>}>
                  <Typography>Accepted price {formatMoney(b.acceptedQuotationAmount)}</Typography>
                </Section>
              )}
              <Section title="Quotations">
                {(quotations.data?.items ?? []).length === 0 ? (
                  <Typography color="text.secondary">None yet.</Typography>
                ) : (
                  <Stack spacing={1.25}>
                    {quotations.data!.items.map((q) => (
                      <Stack key={q.quotationId} direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center', cursor: 'pointer' }} onClick={() => navigate(`/quotations/${q.quotationId}`)}>
                        <Box>
                          <Typography sx={{ fontWeight: 650 }}>{formatMoney(q.totalAmount)}</Typography>
                          <Typography variant="body2" color="text.secondary">v{q.versionNo}, {q.quotationNumber}</Typography>
                        </Box>
                        <StatusChip status={quotationStatus[q.quotationStatusId]} />
                      </Stack>
                    ))}
                  </Stack>
                )}
              </Section>
              <Section title="History">{history.data && <StatusTimeline history={history.data} statuses={bookingStatus} />}</Section>
            </Grid>
          </Grid>

          {dialog.is('quote') && <QuotationBuilder booking={b} onClose={dialog.close} onCreated={(qid) => { dialog.close(); refresh(); navigate(`/quotations/${qid}`); }} />}
          {dialog.is('assign') && <AssignTripDialog booking={b} onClose={dialog.close} onAssigned={(tid) => { dialog.close(); refresh(); navigate(`/trips/${tid}`); }} />}
          {dialog.is('invoice') && <InvoiceDialog bookingId={id} onClose={dialog.close} onCreated={(iid) => { dialog.close(); refresh(); navigate(`/invoices/${iid}`); }} />}
          <ReasonDialog open={dialog.is('hold')} title="Put the booking on hold?" confirmLabel="Hold booking" onClose={dialog.close} onSubmit={async (r) => { await bookingApi.action(id, 'hold', r); notify('Booking on hold'); refresh(); }} />
          <ReasonDialog open={dialog.is('reject')} title="Reject this booking?" description="The customer sees this reason." confirmLabel="Reject booking" destructive onClose={dialog.close} onSubmit={async (r) => { await bookingApi.action(id, 'reject', r); notify('Booking rejected'); refresh(); }} />
          <ReasonDialog open={dialog.is('cancel')} title="Cancel this booking?" description="The customer is notified with this reason." confirmLabel="Cancel booking" destructive onClose={dialog.close} onSubmit={async (r) => { await bookingApi.cancel(id, r); notify('Booking cancelled'); refresh(); }} />
        </>
      )}
    </QueryView>
  );
}

/** Builds a quotation from the rate card with a live price preview; discounts above the threshold need approval rights. */
function QuotationBuilder({ booking, onClose, onCreated }: { booking: BookingDetails; onClose: () => void; onCreated: (id: number) => void }) {
  const { can } = useAuth();
  const [inputs, setInputs] = useState({
    distanceKm: booking.estimatedDistanceKm ?? 0,
    includeLoading: true,
    includeUnloading: true,
    waitingHours: 0,
    tollAmount: 0,
    isNightMovement: false,
    discountAmount: 0,
    manualAdjustment: 0,
    manualAdjustmentReason: '',
    validityHours: 48,
    notes: '',
  });
  const [preview, setPreview] = useState<PriceEstimate | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const pricingInputs = useMemo(
    () => ({
      bookingId: booking.bookingId,
      distanceKm: Number(inputs.distanceKm) || undefined,
      includeLoading: inputs.includeLoading,
      includeUnloading: inputs.includeUnloading,
      waitingHours: Number(inputs.waitingHours) || 0,
      tollAmount: Number(inputs.tollAmount) || 0,
      isNightMovement: inputs.isNightMovement,
      discountAmount: Number(inputs.discountAmount) || 0,
      manualAdjustment: Number(inputs.manualAdjustment) || 0,
      manualAdjustmentReason: inputs.manualAdjustmentReason || undefined,
    }),
    [booking.bookingId, inputs],
  );

  useEffect(() => {
    const t = window.setTimeout(() => {
      quotationApi
        .preview(pricingInputs)
        .then((p) => {
          setPreview(p);
          setError('');
        })
        .catch((e) => setError(toApiError(e).message));
    }, 400);
    return () => window.clearTimeout(t);
  }, [pricingInputs]);

  const set = (key: keyof typeof inputs) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setInputs((s) => ({ ...s, [key]: e.target.type === 'checkbox' ? e.target.checked : e.target.value }));

  const create = async (sendImmediately: boolean) => {
    setBusy(true);
    try {
      const created = await quotationApi.create({ ...pricingInputs, validityHours: Number(inputs.validityHours) || undefined, notes: inputs.notes || undefined, sendImmediately });
      onCreated(created.id);
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Quotation for {booking.bookingNumber}</DialogTitle>
      <DialogContent>
        <Grid container spacing={3} sx={{ mt: 0 }}>
          <Grid size={{ xs: 12, md: 6 }}>
            <Grid container spacing={1.75}>
              <Grid size={6}><TextField label="Distance (km)" type="number" value={inputs.distanceKm} onChange={set('distanceKm')} /></Grid>
              <Grid size={6}><TextField label="Toll charges (₹)" type="number" value={inputs.tollAmount} onChange={set('tollAmount')} /></Grid>
              <Grid size={6}><TextField label="Expected waiting (hours)" type="number" value={inputs.waitingHours} onChange={set('waitingHours')} /></Grid>
              <Grid size={6}><TextField label="Valid for (hours)" type="number" value={inputs.validityHours} onChange={set('validityHours')} /></Grid>
              <Grid size={12}>
                <FormControlLabel control={<Checkbox checked={inputs.includeLoading} onChange={set('includeLoading')} />} label="Loading" />
                <FormControlLabel control={<Checkbox checked={inputs.includeUnloading} onChange={set('includeUnloading')} />} label="Unloading" />
                <FormControlLabel control={<Checkbox checked={inputs.isNightMovement} onChange={set('isNightMovement')} />} label="Night movement" />
              </Grid>
              <Grid size={6}><TextField label="Discount (₹)" type="number" value={inputs.discountAmount} onChange={set('discountAmount')} helperText={can(P.ApproveQuotations) ? undefined : 'Large discounts need approval rights'} /></Grid>
              <Grid size={6}><TextField label="Manual adjustment (₹)" type="number" value={inputs.manualAdjustment} onChange={set('manualAdjustment')} /></Grid>
              {Number(inputs.manualAdjustment) !== 0 && (
                <Grid size={12}><TextField label="Reason for adjustment" value={inputs.manualAdjustmentReason} onChange={set('manualAdjustmentReason')} /></Grid>
              )}
              <Grid size={12}><TextField label="Note for the customer (optional)" value={inputs.notes} onChange={set('notes')} multiline rows={2} /></Grid>
            </Grid>
          </Grid>
          <Grid size={{ xs: 12, md: 6 }}>
            <Typography variant="h6" component="h3">Price</Typography>
            {error && <Alert severity="warning" sx={{ my: 1 }}>{error}</Alert>}
            {preview && (
              <Table size="small">
                <TableBody>
                  {preview.lines.map((l, i) => (
                    <TableRow key={i}>
                      <TableCell sx={{ pl: 0 }}>{l.description}</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(l.amount)}</TableCell>
                    </TableRow>
                  ))}
                  <TableRow>
                    <TableCell sx={{ pl: 0 }}>GST {preview.taxPercent}%</TableCell>
                    <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(preview.taxAmount)}</TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell sx={{ pl: 0, fontWeight: 800 }}>Total</TableCell>
                    <TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>{formatMoney(preview.totalAmount)}</TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            )}
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button onClick={() => create(false)} disabled={busy || !preview}>Save draft</Button>
        <Button variant="contained" onClick={() => create(true)} disabled={busy || !preview}>Send to customer</Button>
      </DialogActions>
    </Dialog>
  );
}

/** Picks an available verified vehicle of the booked type and a driver who can drive for its owner. */
function AssignTripDialog({ booking, onClose, onAssigned }: { booking: BookingDetails; onClose: () => void; onAssigned: (tripId: number) => void }) {
  const [pickup, setPickup] = useState(toLocalInput(booking.requestedPickupDateUtc));
  const [delivery, setDelivery] = useState(toLocalInput(new Date(Date.parse(booking.requestedPickupDateUtc) + 8 * 3600_000).toISOString()));
  const [vehicleId, setVehicleId] = useState<number | ''>('');
  const [driverId, setDriverId] = useState<number | ''>('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const pickupUtc = fromLocalInput(pickup);

  const vehicles = useQuery({
    queryKey: ['available-vehicles', booking.vehicleTypeId, booking.totalWeightKg, pickupUtc],
    queryFn: () => tripApi.availableVehicles(booking.vehicleTypeId, booking.totalWeightKg, pickupUtc),
  });
  const vehicle = vehicles.data?.find((v) => v.vehicleId === vehicleId);
  const drivers = useQuery({
    queryKey: ['available-drivers', vehicle?.ownerId, pickupUtc],
    queryFn: () => tripApi.availableDrivers(vehicle!.ownerId, pickupUtc),
    enabled: !!vehicle,
  });

  const assign = async () => {
    setError('');
    if (!vehicleId || !driverId) return setError('Choose a vehicle and a driver.');
    setBusy(true);
    try {
      const created = await tripApi.create({ bookingId: booking.bookingId, vehicleId, driverId, plannedPickupDateUtc: pickupUtc, plannedDeliveryDateUtc: fromLocalInput(delivery) });
      onAssigned(created.id);
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Assign a truck to {booking.bookingNumber}</DialogTitle>
      <DialogContent>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {booking.vehicleTypeName}, {formatKg(booking.totalWeightKg)}, {booking.pickupCityName} → {booking.deliveryCityName}
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Grid container spacing={2}>
          <Grid size={6}><TextField label="Planned pickup (IST)" type="datetime-local" value={pickup} onChange={(e) => setPickup(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} /></Grid>
          <Grid size={6}><TextField label="Planned delivery (IST)" type="datetime-local" value={delivery} onChange={(e) => setDelivery(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} /></Grid>
          <Grid size={12}>
            <TextField select label="Vehicle" value={vehicleId} onChange={(e) => { setVehicleId(Number(e.target.value)); setDriverId(''); }} helperText={vehicles.data?.length === 0 ? 'No verified vehicle of this type is free on that date.' : undefined}>
              {(vehicles.data ?? []).map((v) => (
                <MenuItem key={v.vehicleId} value={v.vehicleId}>
                  {v.vehicleNumber} — {v.ownerName}, {formatKg(v.capacityKg)}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid size={12}>
            <TextField select label="Driver" value={driverId} onChange={(e) => setDriverId(Number(e.target.value))} disabled={!vehicle} helperText={vehicle && drivers.data?.length === 0 ? "No available driver for this owner. Ask the owner to add one." : undefined}>
              {(drivers.data ?? []).map((d) => (
                <MenuItem key={d.driverId} value={d.driverId}>
                  {d.fullName}, {d.phoneNumber}{d.ownerId ? '' : ' (independent)'}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={assign} disabled={busy}>Assign and notify</Button>
      </DialogActions>
    </Dialog>
  );
}

function InvoiceDialog({ bookingId, onClose, onCreated }: { bookingId: number; onClose: () => void; onCreated: (id: number) => void }) {
  const [terms, setTerms] = useState('7');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Issue the invoice</DialogTitle>
      <DialogContent>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          The invoice is built from the accepted quotation. Proof of delivery must already be uploaded.
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <TextField label="Payment terms (days)" type="number" value={terms} onChange={(e) => setTerms(e.target.value)} />
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          variant="contained"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            try {
              onCreated((await financeApi.createInvoice(bookingId, Number(terms))).id);
            } catch (e) {
              setError(toApiError(e).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          Issue invoice
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export function QuotationsPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  return (
    <>
      <PageHeader title="Quotations" />
      <DataTable<QuotationListItem>
        queryKey={['quotations', status]}
        fetcher={(q) => quotationApi.list({ ...q, status: status ? Number(status) : undefined })}
        rowKey={(q) => q.quotationId}
        onRowClick={(q) => navigate(`/quotations/${q.quotationId}`)}
        searchPlaceholder="Quotation or booking number"
        filters={<FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(quotationStatus)} />}
        empty={{ title: 'No quotations match' }}
        columns={[
          { header: 'Quotation', primary: true, render: (q) => <Typography sx={{ fontWeight: 650 }}>{q.quotationNumber}</Typography> },
          { header: 'Booking', render: (q) => <PlateTag size="small">{q.bookingNumber}</PlateTag> },
          { header: 'Customer', primary: true, render: (q) => q.customerName },
          { header: 'Version', render: (q) => `v${q.versionNo}` },
          { header: 'Total', primary: true, align: 'right', render: (q) => formatMoney(q.totalAmount) },
          { header: 'Valid till', render: (q) => formatDateTime(q.validityDateUtc) },
          { header: 'Status', primary: true, render: (q) => <StatusChip status={quotationStatus[q.quotationStatusId]} /> },
        ]}
      />
    </>
  );
}

export function QuotationDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['quotation', id], queryFn: () => quotationApi.get(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [withdraw, setWithdraw] = useState(false);
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['quotation', id] });

  return (
    <QueryView query={query}>
      {({ quotation: q, charges, availableActions: a }) => (
        <>
          <PageHeader
            back={back(`/bookings/${q.bookingId}`, `Booking ${q.bookingNumber}`)}
            title={`Quotation ${q.quotationNumber}`}
            subtitle={<StatusChip status={quotationStatus[q.quotationStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('Send') && (
                  <Button variant="contained" onClick={async () => { try { await quotationApi.send(id); notify('Sent to the customer'); refresh(); } catch (e) { notify(toApiError(e).message, 'error'); } }}>
                    Send to customer
                  </Button>
                )}
                {a.includes('Withdraw') && <Button color="error" onClick={() => setWithdraw(true)}>Withdraw</Button>}
              </>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Charges">
                <Table size="small">
                  <TableBody>
                    {charges.map((c) => (
                      <TableRow key={c.quotationChargeId}>
                        <TableCell sx={{ pl: 0 }}>{c.description}</TableCell>
                        <TableCell align="right">{c.quantity !== 1 ? `${c.quantity} × ${formatMoney(c.unitRate)}` : ''}</TableCell>
                        <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(c.amount)}</TableCell>
                      </TableRow>
                    ))}
                    <TableRow>
                      <TableCell sx={{ pl: 0 }} colSpan={2}>Sub-total</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(q.subTotal)}</TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }} colSpan={2}>GST {q.taxPercent}%</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(q.taxAmount)}</TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0, fontWeight: 800 }} colSpan={2}>Total</TableCell>
                      <TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>{formatMoney(q.totalAmount)}</TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="Details">
                <DetailList
                  columns={1}
                  items={[
                    ['Customer', q.customerName],
                    ['Distance', formatKm(q.distanceKm)],
                    ['Version', `v${q.versionNo}`],
                    ['Valid till', formatDateTime(q.validityDateUtc)],
                    ['Sent', formatDateTime(q.sentDateUtc)],
                    ['Customer responded', formatDateTime(q.respondedDateUtc)],
                    ['Rejection reason', q.rejectionReason],
                    ['Note to customer', q.notes],
                    ['Created', formatDate(q.createdDateUtc)],
                  ]}
                />
              </Section>
            </Grid>
          </Grid>
          <Divider />
          <ReasonDialog open={withdraw} title="Withdraw this quotation?" confirmLabel="Withdraw" destructive onClose={() => setWithdraw(false)} onSubmit={async (r) => { await quotationApi.withdraw(id, r); notify('Quotation withdrawn'); refresh(); }} />
        </>
      )}
    </QueryView>
  );
}
