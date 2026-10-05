import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Grid,
  MenuItem,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableRow,
  Typography,
} from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { dashboardApi, financeApi, ownerApi, tripApi } from '../../api/endpoints';
import type { DriverListItem, SettlementListItem, TripListItem, Vehicle, VehicleListItem } from '../../api/types';
import { toApiError } from '../../api/client';
import { DataTable } from '../../components/DataTable';
import { DocumentsPanel } from '../../components/Documents';
import { applyApiErrors, FormField, useNotify } from '../../components/Forms';
import { DetailList, PageHeader, QueryView, Section, StatTile } from '../../components/Layout';
import { PlateTag, StatusChip } from '../../components/PlateTag';
import { useReference } from '../../components/ReferenceFields';
import { StatusTimeline } from '../../components/Timeline';
import { TripTrackingPanel } from '../../components/TripTracking';
import { daysUntil, formatDate, formatDateOnly, formatDateTime, formatKg, formatMoney } from '../../lib/format';
import { availabilityStatus, settlementStatus, tripStatus, verificationStatus } from '../../lib/statuses';
import * as v from '../../lib/validation';

const backTo = (to: string, label: string) => (
  <Button component={RouterLink} to={to} startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
    {label}
  </Button>
);

// ---------------- overview ----------------

export function OwnerDashboardPage() {
  const summary = useQuery({ queryKey: ['dashboard', 'owner'], queryFn: dashboardApi.owner });
  const profile = useQuery({ queryKey: ['owner', 'me'], queryFn: ownerApi.me });
  const navigate = useNavigate();

  return (
    <>
      <PageHeader title="Your fleet today" actions={<Button component={RouterLink} to="/owner/vehicles?add=1" variant="contained">Add a vehicle</Button>} />
      {profile.data && profile.data.owner.verificationStatusId !== 3 && (
        <Alert severity={profile.data.owner.verificationStatusId === 4 ? 'error' : 'info'} sx={{ mb: 2.5 }} action={<Button color="inherit" component={RouterLink} to="/owner/profile">Open profile</Button>}>
          {profile.data.owner.verificationStatusId === 4
            ? `Verification was not approved: ${profile.data.owner.verificationRemarks ?? 'please check your documents'}.`
            : 'Upload your PAN, Aadhaar and a cancelled cheque so we can verify your account and start sending you loads.'}
        </Alert>
      )}
      <QueryView query={summary}>
        {(s) => (
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 6, md: 3 }}>
              <StatTile label="Vehicles available" value={`${s.availableVehicles} of ${s.totalVehicles}`} hint={s.vehiclesPendingVerification ? `${s.vehiclesPendingVerification} waiting for verification` : undefined} />
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <StatTile label="Trips on the road" value={s.activeTrips} />
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <StatTile label="Payout pending" value={formatMoney(s.pendingSettlementAmount)} />
            </Grid>
            <Grid size={{ xs: 6, md: 3 }}>
              <StatTile label="Earned so far" value={formatMoney(s.totalEarned)} />
            </Grid>
          </Grid>
        )}
      </QueryView>
      <Section title="Active trips" action={<Button component={RouterLink} to="/owner/trips">All trips</Button>}>
        <DataTable<TripListItem>
          queryKey={['trips', 'active']}
          fetcher={(q) => tripApi.list({ ...q, activeOnly: true })}
          rowKey={(t) => t.tripId}
          onRowClick={(t) => navigate(`/owner/trips/${t.tripId}`)}
          empty={{ title: 'No trips on the road', text: 'New trips appear here as soon as operations assign one of your vehicles.' }}
          columns={tripColumns}
        />
      </Section>
    </>
  );
}

const tripColumns = [
  { header: 'Trip', primary: true, render: (t: TripListItem) => <PlateTag size="small">{t.tripNumber}</PlateTag> },
  { header: 'Route', primary: true, render: (t: TripListItem) => `${t.pickupCityName} to ${t.deliveryCityName}` },
  { header: 'Vehicle', render: (t: TripListItem) => t.vehicleNumber },
  { header: 'Driver', primary: true, render: (t: TripListItem) => t.driverName },
  { header: 'Pickup', render: (t: TripListItem) => formatDateTime(t.plannedPickupDateUtc) },
  { header: 'Status', primary: true, render: (t: TripListItem) => <StatusChip status={tripStatus[t.tripStatusId]} /> },
];

// ---------------- vehicles ----------------

const vehicleSchema = z.object({
  vehicleNumber: v.vehicleNumber,
  vehicleTypeId: z.coerce.number().min(1, 'Choose the vehicle type'),
  manufacturer: v.safeText(100).pipe(z.string().min(2, 'Enter the make')),
  model: v.safeText(100).pipe(z.string().min(1, 'Enter the model')),
  manufactureYear: z.coerce.number().int().min(1980, 'Check the year').max(new Date().getFullYear() + 1, 'Check the year'),
  capacityKg: z.coerce.number().positive('Enter the payload').max(60000),
  permitNumber: v.safeText(50).optional(),
  permitExpiryDate: z.string().optional(),
  insuranceNumber: v.safeText(50).optional(),
  insuranceExpiryDate: z.string().optional(),
  fitnessExpiryDate: z.string().optional(),
  pucExpiryDate: z.string().optional(),
});
type VehicleForm = z.input<typeof vehicleSchema>;
type VehicleOut = z.output<typeof vehicleSchema>;

export function VehiclesPage() {
  const navigate = useNavigate();
  const [adding, setAdding] = useState(new URLSearchParams(window.location.search).has('add'));
  const queryClient = useQueryClient();

  return (
    <>
      <PageHeader title="Vehicles" actions={<Button variant="contained" onClick={() => setAdding(true)}>Add a vehicle</Button>} />
      <DataTable<VehicleListItem>
        queryKey={['vehicles']}
        fetcher={(q) => ownerApi.vehicles(q)}
        rowKey={(r) => r.vehicleId}
        onRowClick={(r) => navigate(`/owner/vehicles/${r.vehicleId}`)}
        searchPlaceholder="Search by registration number"
        empty={{ title: 'No vehicles yet', text: 'Add each truck with its RC, permit and insurance details to start receiving trips.', action: <Button variant="contained" onClick={() => setAdding(true)}>Add a vehicle</Button> }}
        columns={[
          { header: 'Registration', primary: true, render: (r) => <PlateTag size="small">{r.vehicleNumber}</PlateTag> },
          { header: 'Type', primary: true, render: (r) => `${r.vehicleTypeName}, ${formatKg(r.capacityKg)}` },
          { header: 'Make', render: (r) => `${r.manufacturer} ${r.model}` },
          { header: 'Insurance till', render: (r) => <ExpiryText date={r.insuranceExpiryDate} /> },
          { header: 'Verification', primary: true, render: (r) => <StatusChip status={verificationStatus[r.verificationStatusId]} /> },
          { header: 'Available', render: (r) => (r.isAvailable ? 'Yes' : 'No') },
        ]}
      />
      {adding && (
        <VehicleDialog
          onClose={() => setAdding(false)}
          onSaved={(id) => {
            setAdding(false);
            queryClient.invalidateQueries({ queryKey: ['vehicles'] });
            if (id) navigate(`/owner/vehicles/${id}`);
          }}
        />
      )}
    </>
  );
}

function ExpiryText({ date }: { date: string | null }) {
  const days = daysUntil(date);
  if (!date) return <>—</>;
  return (
    <Typography variant="body2" color={days != null && days < 0 ? 'error' : days != null && days <= 30 ? 'warning.main' : undefined}>
      {formatDateOnly(date)}
    </Typography>
  );
}

function VehicleDialog({ vehicle, onClose, onSaved }: { vehicle?: Vehicle; onClose: () => void; onSaved: (id?: number) => void }) {
  const reference = useReference();
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<VehicleForm, unknown, VehicleOut>({
    resolver: zodResolver(vehicleSchema),
    defaultValues: {
      vehicleNumber: vehicle?.vehicleNumber ?? '',
      vehicleTypeId: vehicle?.vehicleTypeId ?? 0,
      manufacturer: vehicle?.manufacturer ?? '',
      model: vehicle?.model ?? '',
      manufactureYear: vehicle?.manufactureYear ?? new Date().getFullYear(),
      capacityKg: vehicle?.capacityKg ?? 0,
      permitNumber: vehicle?.permitNumber ?? '',
      permitExpiryDate: vehicle?.permitExpiryDate ?? '',
      insuranceNumber: vehicle?.insuranceNumber ?? '',
      insuranceExpiryDate: vehicle?.insuranceExpiryDate ?? '',
      fitnessExpiryDate: vehicle?.fitnessExpiryDate ?? '',
      pucExpiryDate: vehicle?.pucExpiryDate ?? '',
    },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    const body = {
      ...d,
      permitNumber: d.permitNumber || null,
      insuranceNumber: d.insuranceNumber || null,
      permitExpiryDate: d.permitExpiryDate || null,
      insuranceExpiryDate: d.insuranceExpiryDate || null,
      fitnessExpiryDate: d.fitnessExpiryDate || null,
      pucExpiryDate: d.pucExpiryDate || null,
    };
    try {
      if (vehicle) {
        await ownerApi.updateVehicle(vehicle.vehicleId, { ...body, rowVersion: vehicle.rowVersion });
        notify('Vehicle updated');
        onSaved();
      } else {
        const created = await ownerApi.createVehicle(body);
        notify('Vehicle added. Upload its documents for verification.');
        onSaved(created.id);
      }
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{vehicle ? `Edit ${vehicle.vehicleNumber}` : 'Add a vehicle'}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="vehicleNumber" label="Registration number" disabled={!!vehicle} placeholder="KA 25 AB 1234" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="vehicleTypeId" label="Vehicle type" select>
                <MenuItem value={0} disabled>
                  Choose
                </MenuItem>
                {(reference.data?.vehicleTypes ?? []).map((t) => (
                  <MenuItem key={t.vehicleTypeId} value={t.vehicleTypeId}>
                    {t.name}
                  </MenuItem>
                ))}
              </FormField>
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="capacityKg" label="Payload (kg)" type="number" inputMode="decimal" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="manufacturer" label="Make" placeholder="Tata, Ashok Leyland, Eicher…" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="model" label="Model" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="manufactureYear" label="Year" type="number" inputMode="numeric" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="permitNumber" label="Permit number" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="permitExpiryDate" label="Permit valid till" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="insuranceNumber" label="Insurance policy number" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="insuranceExpiryDate" label="Insurance valid till" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="fitnessExpiryDate" label="Fitness valid till" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="pucExpiryDate" label="PUC valid till" type="date" />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            {vehicle ? 'Save changes' : 'Add vehicle'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

export function VehicleDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['vehicle', id], queryFn: () => ownerApi.vehicle(id) });
  const [editing, setEditing] = useState(false);
  const queryClient = useQueryClient();
  const notify = useNotify();

  return (
    <QueryView query={query}>
      {(vehicle) => (
        <>
          <PageHeader
            back={backTo('/owner/vehicles', 'Vehicles')}
            title={<PlateTag size="large">{vehicle.vehicleNumber}</PlateTag>}
            subtitle={`${vehicle.vehicleTypeName}, ${vehicle.manufacturer} ${vehicle.model}`}
            actions={<Button onClick={() => setEditing(true)}>Edit details</Button>}
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Details" action={<StatusChip status={verificationStatus[vehicle.verificationStatusId]} />}>
                {vehicle.verificationStatusId === 4 && vehicle.verificationRemarks && <Alert severity="error" sx={{ mb: 2 }}>{vehicle.verificationRemarks}</Alert>}
                <DetailList
                  items={[
                    ['Payload', formatKg(vehicle.capacityKg)],
                    ['Year', vehicle.manufactureYear?.toString() ?? '—'],
                    ['Permit', vehicle.permitNumber ? `${vehicle.permitNumber}, till ${formatDateOnly(vehicle.permitExpiryDate)}` : '—'],
                    ['Insurance', vehicle.insuranceNumber ? `${vehicle.insuranceNumber}, till ${formatDateOnly(vehicle.insuranceExpiryDate)}` : '—'],
                    ['Fitness till', formatDateOnly(vehicle.fitnessExpiryDate)],
                    ['PUC till', formatDateOnly(vehicle.pucExpiryDate)],
                  ]}
                />
                <FormControlLabel
                  sx={{ mt: 2 }}
                  control={
                    <Switch
                      checked={vehicle.isAvailable}
                      disabled={vehicle.verificationStatusId !== 3}
                      onChange={async (e) => {
                        try {
                          await ownerApi.setVehicleAvailability(vehicle.vehicleId, e.target.checked, e.target.checked ? 'Back in service' : 'Marked unavailable by owner');
                          notify(e.target.checked ? 'Vehicle is available for trips' : 'Vehicle will not get new trips');
                          queryClient.invalidateQueries({ queryKey: ['vehicle', id] });
                        } catch (err) {
                          notify(toApiError(err).message, 'error');
                        }
                      }}
                    />
                  }
                  label="Available for new trips"
                />
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Documents">
                <DocumentsPanel entity="Vehicle" entityId={vehicle.vehicleId} />
              </Section>
            </Grid>
          </Grid>
          {editing && (
            <VehicleDialog
              vehicle={vehicle}
              onClose={() => setEditing(false)}
              onSaved={() => {
                setEditing(false);
                queryClient.invalidateQueries({ queryKey: ['vehicle', id] });
              }}
            />
          )}
        </>
      )}
    </QueryView>
  );
}

// ---------------- drivers ----------------

const driverSchema = z.object({
  fullName: v.personName,
  email: v.email,
  phoneNumber: v.phone,
  alternatePhoneNumber: v.optionalPhone,
  dateOfBirth: z.string().optional(),
  licenseNumber: z.string().trim().toUpperCase().regex(/^[A-Z]{2}[0-9]{2}[0-9A-Z ]{8,15}$/, 'Enter the licence number as printed, e.g. KA2520190012345'),
  licenseClass: v.safeText(30).pipe(z.string().min(2, 'Enter the licence class, e.g. HGMV')),
  licenseIssueDate: z.string().optional(),
  licenseExpiryDate: z.string().min(1, 'Enter the expiry date'),
  issuingAuthority: v.safeText(100).optional(),
});
type DriverForm = z.infer<typeof driverSchema>;

export function DriversPage() {
  const navigate = useNavigate();
  const [adding, setAdding] = useState(false);
  const queryClient = useQueryClient();

  return (
    <>
      <PageHeader
        title="Drivers"
        subtitle="Drivers you add get an e-mail to set their password, then run trips from their phone."
        actions={<Button variant="contained" onClick={() => setAdding(true)}>Add a driver</Button>}
      />
      <DataTable<DriverListItem>
        queryKey={['drivers']}
        fetcher={(q) => ownerApi.drivers(q)}
        rowKey={(d) => d.driverId}
        onRowClick={(d) => navigate(`/owner/drivers/${d.driverId}`)}
        searchPlaceholder="Search by name or mobile"
        empty={{ title: 'No drivers yet', text: 'Add your drivers with their licence details. They are verified before their first trip.', action: <Button variant="contained" onClick={() => setAdding(true)}>Add a driver</Button> }}
        columns={[
          { header: 'Name', primary: true, render: (d) => <Typography sx={{ fontWeight: 650 }}>{d.fullName}</Typography> },
          { header: 'Mobile', primary: true, render: (d) => d.phoneNumber },
          { header: 'Licence till', render: (d) => <ExpiryText date={d.licenseExpiryDate} /> },
          { header: 'Verification', primary: true, render: (d) => <StatusChip status={verificationStatus[d.verificationStatusId]} /> },
          { header: 'Now', render: (d) => <StatusChip status={availabilityStatus[d.availabilityStatusId]} /> },
        ]}
      />
      {adding && (
        <DriverDialog
          onClose={() => setAdding(false)}
          onSaved={(id) => {
            setAdding(false);
            queryClient.invalidateQueries({ queryKey: ['drivers'] });
            navigate(`/owner/drivers/${id}`);
          }}
        />
      )}
    </>
  );
}

function DriverDialog({ onClose, onSaved }: { onClose: () => void; onSaved: (id: number) => void }) {
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<DriverForm>({
    resolver: zodResolver(driverSchema),
    defaultValues: { fullName: '', email: '', phoneNumber: '', alternatePhoneNumber: '', dateOfBirth: '', licenseNumber: '', licenseClass: 'HGMV', licenseIssueDate: '', licenseExpiryDate: '', issuingAuthority: '' },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      const created = await ownerApi.createDriver({
        ...d,
        alternatePhoneNumber: d.alternatePhoneNumber || null,
        dateOfBirth: d.dateOfBirth || null,
        licenseIssueDate: d.licenseIssueDate || null,
        issuingAuthority: d.issuingAuthority || null,
      });
      notify('Driver added. They will receive an e-mail to set a password.');
      onSaved(created.id);
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>Add a driver</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="fullName" label="Full name (as on licence)" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="dateOfBirth" label="Date of birth" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="email" label="E-mail" type="email" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="phoneNumber" label="Mobile" type="tel" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="alternatePhoneNumber" label="Second number (optional)" type="tel" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="licenseNumber" label="Licence number" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="licenseClass" label="Licence class" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="licenseIssueDate" label="Issued on" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="licenseExpiryDate" label="Valid till" type="date" />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <FormField control={control} name="issuingAuthority" label="RTO (optional)" />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            Add driver
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

export function DriverDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['driver', id], queryFn: () => ownerApi.driver(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();

  return (
    <QueryView query={query}>
      {(d) => (
        <>
          <PageHeader
            back={backTo('/owner/drivers', 'Drivers')}
            title={d.fullName}
            subtitle={`${d.driverNumber}, ${d.phoneNumber}`}
            actions={
              <Button
                color={d.isActive ? 'error' : 'primary'}
                onClick={async () => {
                  try {
                    await ownerApi.setDriverActive(d.driverId, !d.isActive);
                    notify(d.isActive ? 'Driver deactivated' : 'Driver reactivated');
                    queryClient.invalidateQueries({ queryKey: ['driver', id] });
                  } catch (e) {
                    notify(toApiError(e).message, 'error');
                  }
                }}
              >
                {d.isActive ? 'Deactivate driver' : 'Reactivate driver'}
              </Button>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Licence" action={<StatusChip status={verificationStatus[d.verificationStatusId]} />}>
                {d.verificationStatusId === 4 && d.verificationRemarks && <Alert severity="error" sx={{ mb: 2 }}>{d.verificationRemarks}</Alert>}
                <DetailList
                  items={[
                    ['Licence number', d.licenseNumber],
                    ['Class', d.licenseClass],
                    ['Valid till', formatDateOnly(d.licenseExpiryDate)],
                    ['Issued by', d.licenseIssuingAuthority],
                    ['Availability', <StatusChip status={availabilityStatus[d.availabilityStatusId]} />],
                    ['E-mail', d.email],
                  ]}
                />
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Documents">
                <DocumentsPanel entity="Driver" entityId={d.driverId} />
              </Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

// ---------------- trips ----------------

export function OwnerTripsPage() {
  const navigate = useNavigate();
  return (
    <>
      <PageHeader title="Trips" subtitle="Every trip assigned to your vehicles." />
      <DataTable<TripListItem>
        queryKey={['trips']}
        fetcher={(q) => tripApi.list(q)}
        rowKey={(t) => t.tripId}
        onRowClick={(t) => navigate(`/owner/trips/${t.tripId}`)}
        searchPlaceholder="Search by trip, booking or vehicle"
        empty={{ title: 'No trips yet', text: 'Once your vehicles are verified, operations will assign trips that match them.' }}
        columns={tripColumns}
      />
    </>
  );
}

export function TripDetailReadOnly({ backPath }: { backPath: string }) {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['trip', id], queryFn: () => tripApi.get(id) });
  const history = useQuery({ queryKey: ['trip', id, 'history'], queryFn: () => tripApi.history(id) });

  return (
    <QueryView query={query}>
      {({ trip: t }) => (
        <>
          <PageHeader
            back={backTo(backPath, 'Trips')}
            title={
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                <span>
                  {t.pickupCityName} to {t.deliveryCityName}
                </span>
                <PlateTag>{t.tripNumber}</PlateTag>
              </Stack>
            }
            subtitle={<StatusChip status={tripStatus[t.tripStatusId]} size="medium" />}
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Tracking">
                <TripTrackingPanel
                  tripId={t.tripId}
                  active={t.tripStatusId === 2 || t.tripStatusId === 3}
                  pickup={t.pickupLatitude && t.pickupLongitude ? { latitude: t.pickupLatitude, longitude: t.pickupLongitude } : null}
                  delivery={t.deliveryLatitude && t.deliveryLongitude ? { latitude: t.deliveryLatitude, longitude: t.deliveryLongitude } : null}
                />
              </Section>
              <Section title="Trip">
                <DetailList
                  items={[
                    ['Vehicle', <PlateTag size="small">{t.vehicleNumber}</PlateTag>],
                    ['Driver', `${t.driverName}, ${t.driverPhoneNumber}`],
                    ['Customer', t.customerName],
                    ['Goods', `${t.goodsDescription}, ${formatKg(t.totalWeightKg)}`],
                    ['Planned pickup', formatDateTime(t.plannedPickupDateUtc)],
                    ['Picked up', formatDateTime(t.actualPickupDateUtc)],
                    ['Planned delivery', formatDateTime(t.plannedDeliveryDateUtc)],
                    ['Delivered', formatDateTime(t.actualDeliveryDateUtc)],
                    ['Pickup address', t.pickupAddress],
                    ['Delivery address', t.deliveryAddress],
                  ]}
                />
                {t.exceptionReason && t.tripStatusId === 10 && <Alert severity="error" sx={{ mt: 2 }}>Problem reported: {t.exceptionReason}</Alert>}
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="History">{history.data && <StatusTimeline history={history.data} statuses={tripStatus} />}</Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

// ---------------- earnings ----------------

export function EarningsPage() {
  const summary = useQuery({ queryKey: ['earnings'], queryFn: financeApi.earnings });
  const [selected, setSelected] = useState<number | null>(null);

  return (
    <>
      <PageHeader title="Earnings" subtitle="Freight minus platform commission and TDS, paid to your primary bank account after the customer pays." />
      <QueryView query={summary}>
        {(s) => (
          <Grid container spacing={2} sx={{ mb: 3 }}>
            <Grid size={{ xs: 12, sm: 4 }}>
              <StatTile label="Paid to you" value={formatMoney(s.totalEarned)} hint={`${s.completedCount} settlements`} />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <StatTile label="On the way" value={formatMoney(s.pendingAmount)} hint={`${s.pendingCount} settlements`} />
            </Grid>
            <Grid size={{ xs: 12, sm: 4 }}>
              <StatTile label="Last payout" value={s.lastSettlementDateUtc ? formatDate(s.lastSettlementDateUtc) : '—'} />
            </Grid>
          </Grid>
        )}
      </QueryView>
      <DataTable<SettlementListItem>
        queryKey={['settlements']}
        fetcher={(q) => financeApi.settlements(q)}
        rowKey={(r) => r.settlementId}
        onRowClick={(r) => setSelected(r.settlementId)}
        empty={{ title: 'No settlements yet', text: 'Each completed and paid trip is settled to your bank account.' }}
        columns={[
          { header: 'Settlement', primary: true, render: (r) => <Typography sx={{ fontWeight: 650 }}>{r.settlementNumber}</Typography> },
          { header: 'Trip', render: (r) => <PlateTag size="small">{r.tripNumber}</PlateTag> },
          { header: 'Freight', align: 'right', render: (r) => formatMoney(r.grossAmount) },
          { header: 'Commission', align: 'right', render: (r) => formatMoney(r.commissionAmount) },
          { header: 'You receive', primary: true, align: 'right', render: (r) => <Typography sx={{ fontWeight: 700 }}>{formatMoney(r.netAmount)}</Typography> },
          { header: 'Status', primary: true, render: (r) => <StatusChip status={settlementStatus[r.settlementStatusId]} /> },
        ]}
      />
      {selected && <SettlementDialog id={selected} onClose={() => setSelected(null)} />}
    </>
  );
}

function SettlementDialog({ id, onClose }: { id: number; onClose: () => void }) {
  const query = useQuery({ queryKey: ['settlement', id], queryFn: () => financeApi.settlement(id) });
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Settlement statement</DialogTitle>
      <DialogContent>
        <QueryView query={query}>
          {({ settlement: s, items }) => (
            <Box>
              <DetailList
                items={[
                  ['Settlement', s.settlementNumber],
                  ['Trip', s.tripNumber],
                  ['Status', <StatusChip status={settlementStatus[s.settlementStatusId]} />],
                  ['Paid to', s.bankName ? `${s.bankName} ••••${s.accountNumberLast4}` : 'Primary bank account'],
                  ['Paid on', formatDate(s.settlementDateUtc)],
                  ['Bank reference', s.transactionReference],
                ]}
              />
              <Table size="small" sx={{ mt: 2 }}>
                <TableBody>
                  {items.map((i) => (
                    <TableRow key={i.settlementItemId}>
                      <TableCell sx={{ pl: 0 }}>{i.description}</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>
                        {formatMoney(i.amount)}
                      </TableCell>
                    </TableRow>
                  ))}
                  <TableRow>
                    <TableCell sx={{ pl: 0, fontWeight: 800 }}>You receive</TableCell>
                    <TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>
                      {formatMoney(s.netAmount)}
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
              {s.failureReason && <Alert severity="error" sx={{ mt: 2 }}>{s.failureReason}</Alert>}
            </Box>
          )}
        </QueryView>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
