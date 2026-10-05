import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Grid, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { tripApi } from '../api/endpoints';
import type { Trip, TripListItem } from '../api/types';
import { openFile, toApiError } from '../api/client';
import { DataTable } from '../components/DataTable';
import { FilterSelect, statusOptions, useDialog, useUrlFilter } from '../components/Filters';
import { ReasonDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { StatusTimeline } from '../components/Timeline';
import { TripTrackingPanel } from '../components/TripTracking';
import { formatDateTime, formatKg } from '../lib/format';
import { tripStatus } from '../lib/statuses';
import { back } from './BookingPages';

export function TripsPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  const [active, setActive] = useUrlFilter('active');
  return (
    <>
      <PageHeader title="Trips" />
      <DataTable<TripListItem>
        queryKey={['trips', status, active]}
        fetcher={(q) => tripApi.list({ ...q, status: status ? Number(status) : undefined, activeOnly: active === '1' })}
        rowKey={(t) => t.tripId}
        onRowClick={(t) => navigate(`/trips/${t.tripId}`)}
        searchPlaceholder="Trip, booking, vehicle or driver"
        filters={
          <>
            <FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(tripStatus)} />
            <FilterSelect label="Show" value={active} onChange={setActive} options={[{ value: '1', label: 'On the road only' }]} width={180} />
          </>
        }
        empty={{ title: 'No trips match' }}
        columns={[
          { header: 'Trip', primary: true, render: (t) => <PlateTag size="small">{t.tripNumber}</PlateTag> },
          { header: 'Route', primary: true, render: (t) => `${t.pickupCityName} → ${t.deliveryCityName}` },
          { header: 'Customer', render: (t) => t.customerName },
          { header: 'Vehicle', render: (t) => t.vehicleNumber },
          { header: 'Driver', primary: true, render: (t) => t.driverName },
          { header: 'Pickup', render: (t) => formatDateTime(t.plannedPickupDateUtc) },
          { header: 'Status', primary: true, render: (t) => <StatusChip status={tripStatus[t.tripStatusId]} /> },
        ]}
      />
    </>
  );
}

type TripDialog = 'hold' | 'resolve' | 'cancel' | 'vehicle' | 'driver';

export function TripDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['trip', id], queryFn: () => tripApi.get(id) });
  const history = useQuery({ queryKey: ['trip', id, 'history'], queryFn: () => tripApi.history(id) });
  const assignments = useQuery({ queryKey: ['trip', id, 'assignments'], queryFn: () => tripApi.assignments(id) });
  const dialog = useDialog<TripDialog>();
  const queryClient = useQueryClient();
  const notify = useNotify();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['trip', id] });

  return (
    <QueryView query={query}>
      {({ trip: t, availableActions: a }) => (
        <>
          <PageHeader
            back={back('/trips', 'Trips')}
            title={
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                <PlateTag size="large">{t.tripNumber}</PlateTag>
                <span>{t.pickupCityName} → {t.deliveryCityName}</span>
              </Stack>
            }
            subtitle={<StatusChip status={tripStatus[t.tripStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('ReassignVehicle') && <Button onClick={() => dialog.show('vehicle')}>Change vehicle</Button>}
                {a.includes('ReassignDriver') && <Button onClick={() => dialog.show('driver')}>Change driver</Button>}
                {a.includes('Hold') && <Button onClick={() => dialog.show('hold')}>Hold</Button>}
                {a.includes('Resume') && <Button variant="contained" onClick={async () => { try { await tripApi.resume(id); notify('Trip resumed'); refresh(); } catch (e) { notify(toApiError(e).message, 'error'); } }}>Resume</Button>}
                {a.includes('ResolveException') && <Button variant="contained" onClick={() => dialog.show('resolve')}>Resolve exception</Button>}
                {a.includes('Cancel') && <Button color="error" onClick={() => dialog.show('cancel')}>Cancel trip</Button>}
              </>
            }
          />
          {t.tripStatusId === 10 && <Alert severity="error" sx={{ mb: 2 }}>Exception reported: {t.exceptionReason}</Alert>}
          {t.cancellationReason && <Alert severity="info" sx={{ mb: 2 }}>Cancelled: {t.cancellationReason}</Alert>}

          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, lg: 8 }}>
              <Section title="Live tracking">
                <TripTrackingPanel
                  tripId={t.tripId}
                  active={t.tripStatusId === 2 || t.tripStatusId === 3}
                  pickup={t.pickupLatitude && t.pickupLongitude ? { latitude: t.pickupLatitude, longitude: t.pickupLongitude } : null}
                  delivery={t.deliveryLatitude && t.deliveryLongitude ? { latitude: t.deliveryLatitude, longitude: t.deliveryLongitude } : null}
                />
              </Section>
              <Section title="Trip details">
                <DetailList
                  columns={3}
                  items={[
                    ['Booking', <Button component={RouterLink} to={`/bookings/${t.bookingId}`} sx={{ p: 0, minWidth: 0 }}>{t.bookingNumber}</Button>],
                    ['Customer', t.customerName],
                    ['Vehicle', <PlateTag size="small">{t.vehicleNumber}</PlateTag>],
                    ['Owner', t.ownerName],
                    ['Driver', `${t.driverName}, ${t.driverPhoneNumber}`],
                    ['Load', `${t.goodsDescription}, ${formatKg(t.totalWeightKg)}`],
                    ['Planned pickup', formatDateTime(t.plannedPickupDateUtc)],
                    ['Picked up', formatDateTime(t.actualPickupDateUtc)],
                    ['Start odometer', t.startOdometer?.toString() ?? '—'],
                    ['Planned delivery', formatDateTime(t.plannedDeliveryDateUtc)],
                    ['Delivered', formatDateTime(t.actualDeliveryDateUtc)],
                    ['End odometer', t.endOdometer?.toString() ?? '—'],
                  ]}
                />
                <Grid container spacing={3} sx={{ mt: 1 }}>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant="body2" color="text.secondary">Pickup</Typography>
                    <Typography>{t.pickupAddress}</Typography>
                    <Typography variant="body2">{t.pickupContactName}, {t.pickupContactPhone}</Typography>
                  </Grid>
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant="body2" color="text.secondary">Delivery</Typography>
                    <Typography>{t.deliveryAddress}</Typography>
                    <Typography variant="body2">{t.deliveryContactName}, {t.deliveryContactPhone}</Typography>
                  </Grid>
                </Grid>
              </Section>
              {t.hasProofOfDelivery && <PodSection tripId={t.tripId} />}
            </Grid>
            <Grid size={{ xs: 12, lg: 4 }}>
              <Section title="Status history">{history.data && <StatusTimeline history={history.data} statuses={tripStatus} />}</Section>
              <Section title="Assignments">
                <Stack spacing={1}>
                  {(assignments.data ?? []).map((x) => (
                    <Box key={x.tripAssignmentId}>
                      <Typography sx={{ fontWeight: 600 }}>
                        {x.assignmentTypeId === 1 ? `Vehicle ${x.vehicleNumber}` : `Driver ${x.driverName}`}
                        {x.releasedDateUtc ? ' (replaced)' : ''}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {formatDateTime(x.assignedDateUtc)} by {x.assignedByName}{x.reason ? `: ${x.reason}` : ''}
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              </Section>
            </Grid>
          </Grid>

          <ReasonDialog open={dialog.is('hold')} title="Put the trip on hold?" confirmLabel="Hold trip" onClose={dialog.close} onSubmit={async (r) => { await tripApi.hold(id, r); notify('Trip on hold'); refresh(); }} />
          <ReasonDialog open={dialog.is('resolve')} title="Resolve the exception" description="Record what was done. The trip returns to where it was before the problem." label="Resolution" confirmLabel="Resolve" onClose={dialog.close} onSubmit={async (r) => { await tripApi.resolveException(id, r); notify('Exception resolved'); refresh(); }} />
          <ReasonDialog open={dialog.is('cancel')} title="Cancel this trip?" description="The vehicle and driver are released and the booking returns to Confirmed for a new assignment." confirmLabel="Cancel trip" destructive onClose={dialog.close} onSubmit={async (r) => { await tripApi.cancel(id, r); notify('Trip cancelled'); refresh(); }} />
          {(dialog.is('vehicle') || dialog.is('driver')) && <ReassignDialog trip={t} kind={dialog.open as 'vehicle' | 'driver'} onClose={dialog.close} onDone={() => { dialog.close(); refresh(); }} />}
        </>
      )}
    </QueryView>
  );
}

function ReassignDialog({ trip, kind, onClose, onDone }: { trip: Trip; kind: 'vehicle' | 'driver'; onClose: () => void; onDone: () => void }) {
  const [selected, setSelected] = useState<number | ''>('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState('');
  const notify = useNotify();
  const options = useQuery({
    queryKey: ['reassign', kind, trip.tripId],
    queryFn: async () =>
      kind === 'vehicle'
        ? (await tripApi.availableVehicles(trip.vehicleTypeId, trip.totalWeightKg, trip.plannedPickupDateUtc)).map((v) => ({ id: v.vehicleId, label: `${v.vehicleNumber} — ${v.ownerName}` }))
        : (await tripApi.availableDrivers(trip.ownerId, trip.plannedPickupDateUtc)).map((d) => ({ id: d.driverId, label: `${d.fullName}, ${d.phoneNumber}` })),
  });

  const save = async () => {
    setError('');
    if (!selected || reason.trim().length < 3) return setError(`Choose the ${kind} and give a reason.`);
    try {
      if (kind === 'vehicle') await tripApi.reassignVehicle(trip.tripId, selected, reason.trim());
      else await tripApi.reassignDriver(trip.tripId, selected, reason.trim());
      notify(kind === 'vehicle' ? 'Vehicle changed' : 'Driver changed and notified');
      onDone();
    } catch (e) {
      setError(toApiError(e).message);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Change {kind}</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField select label={kind === 'vehicle' ? 'New vehicle' : 'New driver'} value={selected} onChange={(e) => setSelected(Number(e.target.value))}>
            {(options.data ?? []).map((o) => (
              <MenuItem key={o.id} value={o.id}>{o.label}</MenuItem>
            ))}
          </TextField>
          <TextField label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={save}>Change {kind}</Button>
      </DialogActions>
    </Dialog>
  );
}

function PodSection({ tripId }: { tripId: number }) {
  const pod = useQuery({ queryKey: ['trip', tripId, 'pod'], queryFn: () => tripApi.pod(tripId) });
  const notify = useNotify();
  return (
    <Section title="Proof of delivery">
      <QueryView query={pod}>
        {({ proofOfDelivery: p, files }) => (
          <>
            <DetailList
              items={[
                ['Received by', `${p.receiverName}${p.receiverPhone ? `, ${p.receiverPhone}` : ''}`],
                ['Delivered', formatDateTime(p.deliveredDateUtc)],
                ['Uploaded by', p.uploadedByName],
                ['Remarks', p.remarks],
              ]}
            />
            <Stack direction="row" spacing={1} sx={{ mt: 2, flexWrap: 'wrap', gap: 1 }}>
              {files.map((f) => (
                <Button key={f.storedFileId} variant="outlined" onClick={() => openFile(tripApi.podFileUrl(tripId, f.storedFileId)).catch((e) => notify(toApiError(e).message, 'error'))}>
                  {f.fileCategory}: {f.originalFileName}
                </Button>
              ))}
              {p.signatureFileId && (
                <Button variant="outlined" onClick={() => openFile(tripApi.podFileUrl(tripId, p.signatureFileId!)).catch((e) => notify(toApiError(e).message, 'error'))}>
                  Signature
                </Button>
              )}
            </Stack>
          </>
        )}
      </QueryView>
    </Section>
  );
}
