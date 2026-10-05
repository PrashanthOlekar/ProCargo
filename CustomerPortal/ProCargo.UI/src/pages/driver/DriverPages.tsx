import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  FormControlLabel,
  Grid,
  Link,
  MenuItem,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import PhoneOutlined from '@mui/icons-material/PhoneOutlined';
import NavigationOutlined from '@mui/icons-material/NavigationOutlined';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardApi, driverApi, tripApi } from '../../api/endpoints';
import type { Trip, TripListItem } from '../../api/types';
import { toApiError } from '../../api/client';
import { DataTable } from '../../components/DataTable';
import { DocumentsPanel } from '../../components/Documents';
import { ReasonDialog, useNotify } from '../../components/Forms';
import { DetailList, PageHeader, QueryView, Section, StatTile } from '../../components/Layout';
import { PlateTag, StatusChip } from '../../components/PlateTag';
import { fileSize, formatDateOnly, formatDateTime, formatKg } from '../../lib/format';
import { tripStatus, verificationStatus } from '../../lib/statuses';
import { palette } from '../../theme';

export function DriverHomePage() {
  const navigate = useNavigate();
  const summary = useQuery({ queryKey: ['dashboard', 'driver'], queryFn: dashboardApi.driver });

  return (
    <>
      <PageHeader title="My trips" />
      <QueryView query={summary}>
        {(s) => (
          <Grid container spacing={1.5} sx={{ mb: 3 }}>
            <Grid size={4}>
              <StatTile label="On the road" value={s.activeTrips} />
            </Grid>
            <Grid size={4}>
              <StatTile label="Coming up" value={s.upcomingTrips} />
            </Grid>
            <Grid size={4}>
              <StatTile label="Completed" value={s.completedTrips} />
            </Grid>
          </Grid>
        )}
      </QueryView>
      <DataTable<TripListItem>
        queryKey={['trips', 'driver']}
        fetcher={(q) => tripApi.list({ ...q, sortBy: 'PickupDate', sortDirection: 'desc' })}
        rowKey={(t) => t.tripId}
        onRowClick={(t) => navigate(`/driver/trips/${t.tripId}`)}
        empty={{ title: 'No trips assigned', text: 'When your owner or the ProCargo desk assigns you a trip, it shows up here with the pickup address and contact.' }}
        columns={[
          { header: 'Trip', primary: true, render: (t) => <PlateTag size="small">{t.tripNumber}</PlateTag> },
          { header: 'Route', primary: true, render: (t) => <Typography sx={{ fontWeight: 650 }}>{t.pickupCityName} to {t.deliveryCityName}</Typography> },
          { header: 'Vehicle', render: (t) => t.vehicleNumber },
          { header: 'Pickup', primary: true, render: (t) => formatDateTime(t.plannedPickupDateUtc) },
          { header: 'Status', primary: true, render: (t) => <StatusChip status={tripStatus[t.tripStatusId]} /> },
        ]}
      />
    </>
  );
}

const mapsLink = (lat: number | null, lng: number | null, address: string) =>
  lat && lng ? `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}` : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}`;

/**
 * The driver's trip screen: one big card for the next step, designed for a phone held in one hand at a loading bay.
 */
export function DriverTripPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['trip', id], queryFn: () => tripApi.get(id) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [problemOpen, setProblemOpen] = useState(false);
  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['trip', id] });
    queryClient.invalidateQueries({ queryKey: ['trips'] });
  };

  return (
    <QueryView query={query}>
      {({ trip: t, availableActions }) => (
        <Box sx={{ maxWidth: 720 }}>
          <PageHeader
            back={
              <Button component={RouterLink} to="/driver" startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
                My trips
              </Button>
            }
            title={
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                <span>
                  {t.pickupCityName} to {t.deliveryCityName}
                </span>
                <PlateTag>{t.vehicleNumber}</PlateTag>
              </Stack>
            }
            subtitle={<StatusChip status={tripStatus[t.tripStatusId]} size="medium" />}
          />

          <NextStep trip={t} actions={availableActions} onChanged={refresh} />

          {availableActions.includes('PostLocation') && <LocationSharing tripId={t.tripId} />}

          <Section title="Pickup" dense>
            <Stop label={t.pickupContactName} phone={t.pickupContactPhone} address={t.pickupAddress} link={mapsLink(t.pickupLatitude, t.pickupLongitude, t.pickupAddress)} />
          </Section>
          <Section title="Delivery" dense>
            <Stop label={t.deliveryContactName} phone={t.deliveryContactPhone} address={t.deliveryAddress} link={mapsLink(t.deliveryLatitude, t.deliveryLongitude, t.deliveryAddress)} />
          </Section>
          <Section title="Load" dense>
            <DetailList
              items={[
                ['Goods', t.goodsDescription],
                ['Weight', `${formatKg(t.totalWeightKg)}, ${t.totalQuantity} pieces`],
                ['Pickup planned', formatDateTime(t.plannedPickupDateUtc)],
                ['Deliver by', formatDateTime(t.plannedDeliveryDateUtc)],
                ['Instructions', t.specialInstructions],
                ['Trip', t.tripNumber],
              ]}
            />
          </Section>

          {availableActions.includes('ReportException') && (
            <Button color="error" onClick={() => setProblemOpen(true)} sx={{ mb: 3 }}>
              Report a problem on the road
            </Button>
          )}
          <ReasonDialog
            open={problemOpen}
            title="Report a problem"
            description="Breakdown, accident, road closure or a dispute at the gate. The ProCargo desk will call you."
            label="What happened?"
            confirmLabel="Send to the desk"
            destructive
            onClose={() => setProblemOpen(false)}
            onSubmit={async (reason) => {
              await tripApi.reportException(t.tripId, reason);
              notify('Reported. The desk has been alerted.');
              refresh();
            }}
          />
        </Box>
      )}
    </QueryView>
  );
}

function Stop({ label, phone, address, link }: { label: string; phone: string; address: string; link: string }) {
  return (
    <Box>
      <Typography sx={{ fontWeight: 600 }}>{address}</Typography>
      <Stack direction="row" spacing={1} sx={{ mt: 1.5, flexWrap: 'wrap', gap: 1 }}>
        <Button variant="outlined" startIcon={<PhoneOutlined />} href={`tel:${phone}`}>
          Call {label.split(' ')[0]}
        </Button>
        <Button variant="outlined" startIcon={<NavigationOutlined />} href={link} target="_blank" rel="noopener noreferrer">
          Directions
        </Button>
      </Stack>
    </Box>
  );
}

function StepCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card sx={{ mb: 2.5, borderColor: palette.teal, borderWidth: 2 }}>
      <CardContent sx={{ p: { xs: 2.5, sm: 3 } }}>
        <Typography variant="h4" component="h2" sx={{ mb: 1.5 }}>
          {title}
        </Typography>
        {children}
      </CardContent>
    </Card>
  );
}

function NextStep({ trip, actions, onChanged }: { trip: Trip; actions: string[]; onChanged: () => void }) {
  if (actions.includes('SendPickupOtp')) return <OtpStep trip={trip} step="pickup" onDone={onChanged} />;
  if (actions.includes('Start')) return <StartStep trip={trip} onDone={onChanged} />;
  if (actions.includes('SendDeliveryOtp')) return <OtpStep trip={trip} step="delivery" onDone={onChanged} />;
  if (actions.includes('UploadProofOfDelivery')) return <PodStep trip={trip} onDone={onChanged} />;
  if (trip.tripStatusId === 9) return <Alert severity="warning" sx={{ mb: 2.5 }}>This trip is on hold. Wait for a call from the ProCargo desk.</Alert>;
  if (trip.tripStatusId === 10) return <Alert severity="error" sx={{ mb: 2.5 }}>Problem reported: {trip.exceptionReason}. The desk will call you.</Alert>;
  if (trip.tripStatusId === 8) return <Alert severity="info" sx={{ mb: 2.5 }}>This trip was cancelled{trip.cancellationReason ? `: ${trip.cancellationReason}` : ''}.</Alert>;
  if (trip.tripStatusId >= 5) return <Alert severity="success" sx={{ mb: 2.5 }}>Delivered and proof uploaded. Thank you!</Alert>;
  return null;
}

function OtpStep({ trip, step, onDone }: { trip: Trip; step: 'pickup' | 'delivery'; onDone: () => void }) {
  const notify = useNotify();
  const [sentTo, setSentTo] = useState<string | null>(null);
  const [testOtp, setTestOtp] = useState<string | undefined>();
  const [otp, setOtp] = useState('');
  const [odometer, setOdometer] = useState('');
  const [receiver, setReceiver] = useState(step === 'delivery' ? trip.deliveryContactName : '');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const contact = step === 'pickup' ? trip.pickupContactName : trip.deliveryContactName;

  const send = async () => {
    setError('');
    setBusy(true);
    try {
      const result = await tripApi.sendOtp(trip.tripId, step);
      setSentTo(result.sentTo);
      setTestOtp(result.testOtp);
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  const verify = async () => {
    setError('');
    if (!/^\d{6}$/.test(otp)) return setError('Enter the 6-digit OTP.');
    setBusy(true);
    try {
      const position = await currentPosition();
      await tripApi.verifyOtp(trip.tripId, step, {
        otp,
        odometer: odometer ? Number(odometer) : undefined,
        receiverName: step === 'delivery' ? receiver : undefined,
        latitude: position?.latitude,
        longitude: position?.longitude,
      });
      notify(step === 'pickup' ? 'Pickup confirmed' : 'Delivery confirmed');
      onDone();
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <StepCard title={step === 'pickup' ? 'Confirm pickup' : 'Confirm delivery'}>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        {sentTo
          ? `We sent a 6-digit code to ${contact} (${sentTo}). Ask them to read it out.`
          : step === 'pickup'
            ? `When the goods are loaded, send a code to ${contact}. The trip starts only after you enter it.`
            : `At the delivery point, send a code to ${contact} and enter it after unloading.`}
      </Typography>
      {testOtp && <Alert severity="info" sx={{ mb: 2 }}>Test environment: the code is {testOtp}</Alert>}
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      {!sentTo ? (
        <Button variant="contained" size="large" fullWidth onClick={send} disabled={busy}>
          Send code to {contact.split(' ')[0]}
        </Button>
      ) : (
        <Stack spacing={2}>
          <TextField
            label="6-digit code"
            value={otp}
            onChange={(e) => setOtp(e.target.value.replace(/\D/g, '').slice(0, 6))}
            slotProps={{ htmlInput: { inputMode: 'numeric', autoComplete: 'one-time-code', style: { fontSize: '1.6rem', letterSpacing: '0.4em' } } }}
            autoFocus
          />
          {step === 'delivery' && <TextField label="Received by" value={receiver} onChange={(e) => setReceiver(e.target.value)} />}
          <TextField label="Odometer reading (optional)" value={odometer} onChange={(e) => setOdometer(e.target.value.replace(/\D/g, ''))} slotProps={{ htmlInput: { inputMode: 'numeric' } }} />
          <Button variant="contained" size="large" onClick={verify} disabled={busy}>
            {busy ? 'Checking…' : step === 'pickup' ? 'Confirm pickup' : 'Confirm delivery'}
          </Button>
          <Button onClick={send} disabled={busy}>
            Send a new code
          </Button>
        </Stack>
      )}
    </StepCard>
  );
}

function StartStep({ trip, onDone }: { trip: Trip; onDone: () => void }) {
  const notify = useNotify();
  const [busy, setBusy] = useState(false);
  return (
    <StepCard title="Start the trip">
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Goods are loaded and confirmed. Start the trip when you leave {trip.pickupCityName}; the customer is told you are on the way.
      </Typography>
      <Button
        variant="contained"
        size="large"
        fullWidth
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          try {
            await tripApi.start(trip.tripId);
            notify('Trip started. Drive safe!');
            onDone();
          } catch (e) {
            notify(toApiError(e).message, 'error');
          } finally {
            setBusy(false);
          }
        }}
      >
        Start trip to {trip.deliveryCityName}
      </Button>
    </StepCard>
  );
}

const MAX_PHOTOS = 5;

function PodStep({ trip, onDone }: { trip: Trip; onDone: () => void }) {
  const notify = useNotify();
  const [receiverName, setReceiverName] = useState(trip.deliveryContactName);
  const [receiverPhone, setReceiverPhone] = useState(trip.deliveryContactPhone);
  const [remarks, setRemarks] = useState('');
  const [photos, setPhotos] = useState<File[]>([]);
  const [signature, setSignature] = useState<File | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const photoInput = useRef<HTMLInputElement>(null);
  const signatureInput = useRef<HTMLInputElement>(null);

  const submit = async () => {
    setError('');
    if (receiverName.trim().length < 2) return setError("Enter the receiver's name.");
    if (photos.length === 0 && !signature) return setError('Add at least one photo of the delivered goods or the signed challan.');
    const tooBig = [...photos, ...(signature ? [signature] : [])].find((f) => f.size > 10 * 1024 * 1024);
    if (tooBig) return setError(`${tooBig.name} is larger than 10 MB.`);

    const form = new FormData();
    form.append('receiverName', receiverName.trim());
    if (receiverPhone) form.append('receiverPhone', receiverPhone);
    if (remarks) form.append('remarks', remarks.trim());
    const position = await currentPosition();
    if (position) {
      form.append('latitude', position.latitude.toFixed(6));
      form.append('longitude', position.longitude.toFixed(6));
    }
    photos.forEach((p) => form.append('photos', p));
    if (signature) form.append('signature', signature);

    setBusy(true);
    try {
      await tripApi.uploadPod(trip.tripId, form);
      notify('Proof of delivery uploaded');
      onDone();
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <StepCard title="Upload proof of delivery">
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Photograph the unloaded goods and the signed challan or LR copy. These close the trip and start your owner's payout.
      </Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Stack spacing={2}>
        <TextField label="Received by" value={receiverName} onChange={(e) => setReceiverName(e.target.value)} />
        <TextField label="Receiver mobile" value={receiverPhone} onChange={(e) => setReceiverPhone(e.target.value)} type="tel" />
        <TextField label="Remarks (optional)" value={remarks} onChange={(e) => setRemarks(e.target.value)} multiline rows={2} placeholder="e.g. 2 cartons damaged in transit" />
        <input
          ref={photoInput}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          capture="environment"
          multiple
          hidden
          onChange={(e) => setPhotos((current) => [...current, ...Array.from(e.target.files ?? [])].slice(0, MAX_PHOTOS))}
        />
        <input ref={signatureInput} type="file" accept="image/jpeg,image/png,image/webp" capture="environment" hidden onChange={(e) => setSignature(e.target.files?.[0] ?? null)} />
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
          <Button variant="outlined" onClick={() => photoInput.current?.click()} disabled={photos.length >= MAX_PHOTOS}>
            Add photo ({photos.length}/{MAX_PHOTOS})
          </Button>
          <Button variant="outlined" onClick={() => signatureInput.current?.click()}>
            {signature ? 'Retake signature' : 'Photo of signature'}
          </Button>
          {photos.length > 0 && <Button onClick={() => setPhotos([])}>Clear photos</Button>}
        </Stack>
        {(photos.length > 0 || signature) && (
          <Typography variant="body2" color="text.secondary">
            {[...photos, ...(signature ? [signature] : [])].map((f) => `${f.name} (${fileSize(f.size)})`).join(', ')}
          </Typography>
        )}
        <Button variant="contained" size="large" onClick={submit} disabled={busy}>
          {busy ? 'Uploading…' : 'Upload and finish'}
        </Button>
      </Stack>
    </StepCard>
  );
}

function currentPosition(): Promise<{ latitude: number; longitude: number } | null> {
  if (!('geolocation' in navigator)) return Promise.resolve(null);
  return new Promise((resolve) =>
    navigator.geolocation.getCurrentPosition(
      (p) => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
      () => resolve(null),
      { enableHighAccuracy: true, timeout: 8000, maximumAge: 60_000 },
    ),
  );
}

/**
 * Shares the phone's position while the screen is open: points are collected as the phone reports them and sent
 * in batches every 60 seconds (or on leaving the page), so a weak network does not lose the trail.
 */
function LocationSharing({ tripId }: { tripId: number }) {
  const [enabled, setEnabled] = useState(true);
  const [status, setStatus] = useState('');
  const buffer = useRef<{ latitude: number; longitude: number; recordedDateUtc: string; speedKmph: number | null; heading: number | null; accuracy: number | null }[]>([]);

  useEffect(() => {
    if (!enabled || !('geolocation' in navigator)) return;

    const flush = async () => {
      if (buffer.current.length === 0) return;
      const points = buffer.current.splice(0, 100);
      try {
        await tripApi.addLocations(tripId, points);
        setStatus(`Location shared at ${new Date().toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })}`);
      } catch {
        buffer.current.unshift(...points);
        setStatus('No network — positions will be sent when you are back online.');
      }
    };

    const watch = navigator.geolocation.watchPosition(
      (p) => {
        const last = buffer.current[buffer.current.length - 1];
        if (last && Date.now() - Date.parse(last.recordedDateUtc) < 20_000) return;
        buffer.current.push({
          latitude: Number(p.coords.latitude.toFixed(6)),
          longitude: Number(p.coords.longitude.toFixed(6)),
          recordedDateUtc: new Date(p.timestamp).toISOString(),
          speedKmph: p.coords.speed != null ? Math.round(p.coords.speed * 3.6) : null,
          heading: p.coords.heading != null && !Number.isNaN(p.coords.heading) ? Math.round(p.coords.heading) : null,
          accuracy: p.coords.accuracy != null ? Math.round(p.coords.accuracy) : null,
        });
      },
      () => setStatus('Allow location access in your browser so the customer can follow the truck.'),
      { enableHighAccuracy: true, maximumAge: 15_000 },
    );
    const timer = window.setInterval(flush, 60_000);
    return () => {
      navigator.geolocation.clearWatch(watch);
      window.clearInterval(timer);
      void flush();
    };
  }, [enabled, tripId]);

  return (
    <Section dense>
      <FormControlLabel control={<Switch checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />} label="Share my location with the customer" />
      {status && (
        <Typography variant="body2" color="text.secondary">
          {status}
        </Typography>
      )}
    </Section>
  );
}

export function DriverProfilePage() {
  const query = useQuery({ queryKey: ['driver', 'me'], queryFn: driverApi.me });
  const queryClient = useQueryClient();
  const notify = useNotify();

  return (
    <>
      <PageHeader title="My profile" />
      <QueryView query={query}>
        {(d) => (
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title={d.fullName} action={<StatusChip status={verificationStatus[d.verificationStatusId]} />}>
                <DetailList
                  items={[
                    ['Driver number', d.driverNumber],
                    ['Mobile', d.phoneNumber],
                    ['Works for', d.ownerName ?? 'Independent'],
                    ['Licence', d.licenseNumber],
                    ['Licence valid till', formatDateOnly(d.licenseExpiryDate)],
                  ]}
                />
                <TextField
                  select
                  label="My availability"
                  value={d.availabilityStatusId === 2 ? 'OnTrip' : ({ 1: 'Available', 3: 'OffDuty', 4: 'Unavailable' } as Record<number, string>)[d.availabilityStatusId]}
                  disabled={d.availabilityStatusId === 2}
                  helperText={d.availabilityStatusId === 2 ? 'You are on a trip.' : 'Off duty means you will not be assigned new trips.'}
                  sx={{ mt: 2.5 }}
                  onChange={async (e) => {
                    try {
                      await driverApi.setAvailability(d.driverId, e.target.value);
                      notify('Availability updated');
                      queryClient.invalidateQueries({ queryKey: ['driver', 'me'] });
                    } catch (err) {
                      notify(toApiError(err).message, 'error');
                    }
                  }}
                >
                  <MenuItem value="Available">Available</MenuItem>
                  <MenuItem value="OffDuty">Off duty</MenuItem>
                  <MenuItem value="Unavailable">Unavailable</MenuItem>
                  <MenuItem value="OnTrip" disabled>
                    On a trip
                  </MenuItem>
                </TextField>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Documents">
                <DocumentsPanel entity="Driver" entityId={d.driverId} />
              </Section>
              <Typography variant="body2" color="text.secondary">
                Need to change your licence details? Ask your vehicle owner or <Link component={RouterLink} to="/support">contact support</Link>.
              </Typography>
            </Grid>
          </Grid>
        )}
      </QueryView>
    </>
  );
}
