import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Grid, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bookingApi, partnerApi } from '../api/endpoints';
import type { BankAccountReveal, CustomerListItem, DriverListItem, OwnerListItem, VehicleListItem } from '../api/types';
import { toApiError } from '../api/client';
import { P, useAuth } from '../auth/AuthContext';
import { DataTable } from '../components/DataTable';
import { FilterSelect, useUrlFilter } from '../components/Filters';
import { ConfirmDialog, ReasonDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { DocumentReview } from '../components/Records';
import { daysUntil, formatDate, formatDateOnly, formatDateTime, formatKg } from '../lib/format';
import { availabilityStatus, bookingStatus, verificationStatus } from '../lib/statuses';
import { back } from './BookingPages';

const verificationOptions = [
  { value: 'Pending', label: 'Not verified' },
  { value: 'UnderReview', label: 'Under review' },
  { value: 'Verified', label: 'Verified' },
  { value: 'Rejected', label: 'Rejected' },
];

/** Verify / reject buttons shared by owners, vehicles and drivers. */
function VerificationActions({ statusId, onDecide }: { statusId: number; onDecide: (status: string, remarks?: string) => Promise<unknown> }) {
  const [rejecting, setRejecting] = useState(false);
  const [verifying, setVerifying] = useState(false);
  return (
    <>
      {statusId !== 3 && <Button variant="contained" onClick={() => setVerifying(true)}>Verify</Button>}
      {statusId !== 4 && <Button color="error" onClick={() => setRejecting(true)}>Reject</Button>}
      <ConfirmDialog
        open={verifying}
        title="Mark as verified?"
        body="Check that every mandatory document is present, readable, valid and matches the details entered."
        confirmLabel="Verify"
        onClose={() => setVerifying(false)}
        onConfirm={async () => {
          await onDecide('Verified');
          setVerifying(false);
        }}
      />
      <ReasonDialog open={rejecting} title="Reject verification?" description="The partner sees this reason and can correct their details." confirmLabel="Reject" destructive onClose={() => setRejecting(false)} onSubmit={(r) => onDecide('Rejected', r)} />
    </>
  );
}

function useDecision(refreshKey: unknown[]) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  return async (fn: () => Promise<unknown>, message: string) => {
    try {
      await fn();
      notify(message);
      queryClient.invalidateQueries({ queryKey: refreshKey });
    } catch (e) {
      notify(toApiError(e).message, 'error');
    }
  };
}

// ---------------- customers ----------------

export function CustomersPage() {
  const navigate = useNavigate();
  return (
    <>
      <PageHeader title="Customers" />
      <DataTable<CustomerListItem>
        queryKey={['customers']}
        fetcher={(q) => partnerApi.customers(q)}
        rowKey={(c) => c.customerId}
        onRowClick={(c) => navigate(`/customers/${c.customerId}`)}
        searchPlaceholder="Name, company, e-mail or mobile"
        empty={{ title: 'No customers match' }}
        columns={[
          { header: 'Customer', primary: true, render: (c) => <Typography sx={{ fontWeight: 650 }}>{c.companyName ?? c.fullName}</Typography> },
          { header: 'Number', render: (c) => c.customerNumber },
          { header: 'Contact', primary: true, render: (c) => `${c.fullName}, ${c.phoneNumber}` },
          { header: 'E-mail', render: (c) => c.email },
          { header: 'Bookings', align: 'right', render: (c) => c.bookingCount },
          { header: 'Since', render: (c) => formatDate(c.createdDateUtc) },
          { header: 'Active', primary: true, render: (c) => (c.isActive ? 'Yes' : 'No') },
        ]}
      />
    </>
  );
}

export function CustomerDetailPage() {
  const id = Number(useParams().id);
  const { can } = useAuth();
  const query = useQuery({ queryKey: ['customer', id], queryFn: () => partnerApi.customer(id) });
  const addresses = useQuery({ queryKey: ['customer', id, 'addresses'], queryFn: () => partnerApi.customerAddresses(id) });
  const bookings = useQuery({ queryKey: ['customer', id, 'bookings'], queryFn: () => bookingApi.list({ customerId: id, pageSize: 10 }) });
  const decide = useDecision(['customer', id]);
  const navigate = useNavigate();

  return (
    <QueryView query={query}>
      {(c) => (
        <>
          <PageHeader
            back={back('/customers', 'Customers')}
            title={c.companyName ?? c.fullName}
            subtitle={`${c.customerNumber}${c.isActive ? '' : ' — deactivated'}`}
            actions={can(P.ManageCustomers) && (
              <Button color={c.isActive ? 'error' : 'primary'} onClick={() => decide(() => partnerApi.setCustomerActive(id, !c.isActive), c.isActive ? 'Customer deactivated' : 'Customer reactivated')}>
                {c.isActive ? 'Deactivate' : 'Reactivate'}
              </Button>
            )}
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="Details">
                <DetailList columns={1} items={[['Contact', c.fullName], ['E-mail', c.email], ['Mobile', c.phoneNumber], ['GSTIN', c.gstNumber], ['Customer since', formatDate(c.createdDateUtc)]]} />
              </Section>
              <Section title="Saved addresses">
                {(addresses.data ?? []).map((a) => (
                  <Typography key={a.customerAddressId} variant="body2" sx={{ mb: 1 }}>
                    <b>{a.label}</b>: {a.addressLine1}, {a.cityName} {a.pincode}
                  </Typography>
                ))}
              </Section>
              <Section title="Documents">
                <DocumentReview entity="Customer" entityId={id} canVerify={can(P.ManageCustomers)} />
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Recent bookings">
                <Table size="small">
                  <TableBody>
                    {(bookings.data?.items ?? []).map((b) => (
                      <TableRow key={b.bookingId} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/bookings/${b.bookingId}`)}>
                        <TableCell><PlateTag size="small">{b.bookingNumber}</PlateTag></TableCell>
                        <TableCell>{b.pickupCityName} → {b.deliveryCityName}</TableCell>
                        <TableCell>{formatDate(b.requestedPickupDateUtc)}</TableCell>
                        <TableCell><StatusChip status={bookingStatus[b.bookingStatusId]} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

// ---------------- owners ----------------

export function OwnersPage() {
  const navigate = useNavigate();
  const [verification, setVerification] = useUrlFilter('verification');
  return (
    <>
      <PageHeader title="Vehicle owners" />
      <DataTable<OwnerListItem>
        queryKey={['owners', verification]}
        fetcher={(q) => partnerApi.owners({ ...q, verificationStatus: verification || undefined })}
        rowKey={(o) => o.ownerId}
        onRowClick={(o) => navigate(`/owners/${o.ownerId}`)}
        searchPlaceholder="Name, business or mobile"
        filters={<FilterSelect label="Verification" value={verification} onChange={setVerification} options={verificationOptions} />}
        empty={{ title: 'No owners match' }}
        columns={[
          { header: 'Owner', primary: true, render: (o) => <Typography sx={{ fontWeight: 650 }}>{o.businessName ?? o.fullName}</Typography> },
          { header: 'Number', render: (o) => o.ownerNumber },
          { header: 'Contact', primary: true, render: (o) => `${o.fullName}, ${o.phoneNumber}` },
          { header: 'Vehicles', align: 'right', render: (o) => o.vehicleCount },
          { header: 'Drivers', align: 'right', render: (o) => o.driverCount },
          { header: 'Verification', primary: true, render: (o) => <StatusChip status={verificationStatus[o.verificationStatusId]} /> },
        ]}
      />
    </>
  );
}

export function OwnerDetailPage() {
  const id = Number(useParams().id);
  const { can } = useAuth();
  const query = useQuery({ queryKey: ['owner', id], queryFn: () => partnerApi.owner(id) });
  const decide = useDecision(['owner', id]);
  const [revealed, setRevealed] = useState<BankAccountReveal | null>(null);
  const notify = useNotify();
  const navigate = useNavigate();

  return (
    <QueryView query={query}>
      {({ owner: o, addresses, bankAccounts }) => (
        <>
          <PageHeader
            back={back('/owners', 'Vehicle owners')}
            title={o.businessName ?? o.fullName}
            subtitle={<Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><span>{o.ownerNumber}</span><StatusChip status={verificationStatus[o.verificationStatusId]} /></Stack>}
            actions={
              <>
                {can(P.ApproveOwners) && <VerificationActions statusId={o.verificationStatusId} onDecide={(s, r) => decide(() => partnerApi.verifyOwner(id, s, r), s === 'Verified' ? 'Owner verified' : 'Owner verification rejected')} />}
                {can(P.ManageOwners) && (
                  <Button onClick={() => decide(() => partnerApi.setOwnerActive(id, !o.isActive), o.isActive ? 'Owner deactivated' : 'Owner reactivated')}>
                    {o.isActive ? 'Deactivate' : 'Reactivate'}
                  </Button>
                )}
              </>
            }
          />
          {o.verificationRemarks && o.verificationStatusId === 4 && <Alert severity="error" sx={{ mb: 2 }}>{o.verificationRemarks}</Alert>}
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Owner">
                <DetailList
                  items={[
                    ['Name', o.fullName],
                    ['Type', o.ownerTypeId === 2 ? 'Fleet business' : 'Individual'],
                    ['E-mail', o.email],
                    ['Mobile', o.phoneNumber],
                    ['PAN', o.panLast4 ? `••••••${o.panLast4}` : 'Not provided'],
                    ['GSTIN', o.gstNumber],
                    ['Registration', o.registrationNumber],
                    ['Fleet size', o.fleetSize?.toString() ?? '—'],
                    ['Address', addresses[0] ? `${addresses[0].addressLine1}, ${addresses[0].cityName} ${addresses[0].pincode}` : '—'],
                    ['Joined', formatDate(o.createdDateUtc)],
                  ]}
                />
                <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                  <Button onClick={() => navigate(`/vehicles?ownerId=${id}`)}>Vehicles</Button>
                  <Button onClick={() => navigate(`/drivers?ownerId=${id}`)}>Drivers</Button>
                </Stack>
              </Section>
              <Section title="Payout bank accounts">
                {bankAccounts.length === 0 && <Typography color="text.secondary">No bank account added yet.</Typography>}
                <Stack spacing={1.25}>
                  {bankAccounts.map((b) => (
                    <Box key={b.ownerBankAccountId} sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.5, opacity: b.isActive ? 1 : 0.6 }}>
                      <Stack direction="row" sx={{ justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
                        <Box>
                          <Typography sx={{ fontWeight: 650 }}>
                            {b.bankName} ••••{b.accountNumberLast4}{b.isPrimary ? ' (primary)' : ''}{b.isActive ? '' : ' (removed)'}
                          </Typography>
                          <Typography variant="body2" color="text.secondary">{b.accountHolderName}, IFSC {b.ifscCode}</Typography>
                        </Box>
                        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                          <StatusChip status={verificationStatus[b.verificationStatusId]} />
                          {can(P.ApproveOwners) && b.isActive && b.verificationStatusId !== 3 && (
                            <Button size="small" variant="contained" onClick={() => decide(() => partnerApi.verifyBankAccount(id, b.ownerBankAccountId, 'Verified'), 'Bank account verified')}>
                              Verify
                            </Button>
                          )}
                          {can(P.ManageSettlements) && b.isActive && (
                            <Button size="small" onClick={async () => { try { setRevealed(await partnerApi.revealBankAccount(id, b.ownerBankAccountId)); } catch (e) { notify(toApiError(e).message, 'error'); } }}>
                              Show full number
                            </Button>
                          )}
                        </Stack>
                      </Stack>
                      {revealed?.ownerBankAccountId === b.ownerBankAccountId && (
                        <Alert severity="info" sx={{ mt: 1 }} onClose={() => setRevealed(null)}>
                          Account number {revealed.accountNumber}. This view was recorded in the audit log.
                        </Alert>
                      )}
                    </Box>
                  ))}
                </Stack>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="KYC documents">
                <DocumentReview entity="Owner" entityId={id} canVerify={can(P.ApproveOwners)} />
              </Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

// ---------------- vehicles ----------------

function ExpiryCell({ date }: { date: string | null }) {
  const days = daysUntil(date);
  return (
    <Typography variant="body2" color={days != null && days < 0 ? 'error' : days != null && days <= 30 ? 'warning.main' : undefined}>
      {formatDateOnly(date)}
    </Typography>
  );
}

export function VehiclesPage() {
  const navigate = useNavigate();
  const [verification, setVerification] = useUrlFilter('verification');
  const [ownerId] = useUrlFilter('ownerId');
  const [expiring, setExpiring] = useUrlFilter('expiring');
  const expiringQuery = useQuery({ queryKey: ['vehicles', 'expiring'], queryFn: () => partnerApi.expiringVehicles(30), enabled: expiring === '1' });

  return (
    <>
      <PageHeader title="Vehicles" actions={<Button onClick={() => setExpiring(expiring === '1' ? '' : '1')}>{expiring === '1' ? 'All vehicles' : 'Documents expiring soon'}</Button>} />
      {expiring === '1' ? (
        <Section title="Documents expiring within 30 days">
          <QueryView query={expiringQuery}>
            {(rows) => (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Vehicle</TableCell>
                    <TableCell>Owner</TableCell>
                    <TableCell>Insurance</TableCell>
                    <TableCell>Permit</TableCell>
                    <TableCell>Fitness</TableCell>
                    <TableCell>PUC</TableCell>
                    <TableCell align="right">Days left</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={r.vehicleId} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/vehicles/${r.vehicleId}`)}>
                      <TableCell><PlateTag size="small">{r.vehicleNumber}</PlateTag></TableCell>
                      <TableCell>{r.ownerName}</TableCell>
                      <TableCell><ExpiryCell date={r.insuranceExpiryDate} /></TableCell>
                      <TableCell><ExpiryCell date={r.permitExpiryDate} /></TableCell>
                      <TableCell><ExpiryCell date={r.fitnessExpiryDate} /></TableCell>
                      <TableCell><ExpiryCell date={r.pucExpiryDate} /></TableCell>
                      <TableCell align="right">{r.daysToNextExpiry}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </QueryView>
        </Section>
      ) : (
        <DataTable<VehicleListItem>
          queryKey={['vehicles', verification, ownerId]}
          fetcher={(q) => partnerApi.vehicles({ ...q, verificationStatus: verification || undefined, ownerId: ownerId ? Number(ownerId) : undefined })}
          rowKey={(v) => v.vehicleId}
          onRowClick={(v) => navigate(`/vehicles/${v.vehicleId}`)}
          searchPlaceholder="Registration number"
          filters={<FilterSelect label="Verification" value={verification} onChange={setVerification} options={verificationOptions} />}
          empty={{ title: 'No vehicles match' }}
          columns={[
            { header: 'Vehicle', primary: true, render: (v) => <PlateTag size="small">{v.vehicleNumber}</PlateTag> },
            { header: 'Type', primary: true, render: (v) => `${v.vehicleTypeName}, ${formatKg(v.capacityKg)}` },
            { header: 'Owner', render: (v) => v.ownerName },
            { header: 'Insurance', render: (v) => <ExpiryCell date={v.insuranceExpiryDate} /> },
            { header: 'Permit', render: (v) => <ExpiryCell date={v.permitExpiryDate} /> },
            { header: 'Available', render: (v) => (v.isAvailable ? 'Yes' : 'No') },
            { header: 'Verification', primary: true, render: (v) => <StatusChip status={verificationStatus[v.verificationStatusId]} /> },
          ]}
        />
      )}
    </>
  );
}

export function VehicleDetailPage() {
  const id = Number(useParams().id);
  const { can } = useAuth();
  const query = useQuery({ queryKey: ['vehicle', id], queryFn: () => partnerApi.vehicle(id) });
  const decide = useDecision(['vehicle', id]);

  return (
    <QueryView query={query}>
      {(v) => (
        <>
          <PageHeader
            back={back('/vehicles', 'Vehicles')}
            title={<PlateTag size="large">{v.vehicleNumber}</PlateTag>}
            subtitle={<Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><span>{v.vehicleTypeName}, {v.manufacturer} {v.model}</span><StatusChip status={verificationStatus[v.verificationStatusId]} /></Stack>}
            actions={
              <>
                {can(P.ApproveVehicles) && <VerificationActions statusId={v.verificationStatusId} onDecide={(s, r) => decide(() => partnerApi.verifyVehicle(id, s, r), s === 'Verified' ? 'Vehicle verified' : 'Vehicle verification rejected')} />}
                {can(P.ManageVehicles) && <Button onClick={() => decide(() => partnerApi.setVehicleActive(id, !v.isActive), v.isActive ? 'Vehicle deactivated' : 'Vehicle reactivated')}>{v.isActive ? 'Deactivate' : 'Reactivate'}</Button>}
              </>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Details">
                <DetailList
                  items={[
                    ['Owner', <Button component={RouterLink} to={`/owners/${v.ownerId}`} sx={{ p: 0, minWidth: 0 }}>{v.ownerName}</Button>],
                    ['Payload', formatKg(v.capacityKg)],
                    ['Year', v.manufactureYear?.toString() ?? '—'],
                    ['Available', v.isAvailable ? 'Yes' : 'No'],
                    ['Permit', `${v.permitNumber ?? '—'}, till ${formatDateOnly(v.permitExpiryDate)}`],
                    ['Insurance', `${v.insuranceNumber ?? '—'}, till ${formatDateOnly(v.insuranceExpiryDate)}`],
                    ['Fitness till', formatDateOnly(v.fitnessExpiryDate)],
                    ['PUC till', formatDateOnly(v.pucExpiryDate)],
                  ]}
                />
                {v.verificationRemarks && <Alert severity="info" sx={{ mt: 2 }}>{v.verificationRemarks}</Alert>}
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Documents">
                <DocumentReview entity="Vehicle" entityId={id} canVerify={can(P.ApproveVehicles)} />
              </Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}

// ---------------- drivers ----------------

export function DriversPage() {
  const navigate = useNavigate();
  const [verification, setVerification] = useUrlFilter('verification');
  const [ownerId] = useUrlFilter('ownerId');
  return (
    <>
      <PageHeader title="Drivers" />
      <DataTable<DriverListItem>
        queryKey={['drivers', verification, ownerId]}
        fetcher={(q) => partnerApi.drivers({ ...q, verificationStatus: verification || undefined, ownerId: ownerId ? Number(ownerId) : undefined })}
        rowKey={(d) => d.driverId}
        onRowClick={(d) => navigate(`/drivers/${d.driverId}`)}
        searchPlaceholder="Name, mobile or licence"
        filters={<FilterSelect label="Verification" value={verification} onChange={setVerification} options={verificationOptions} />}
        empty={{ title: 'No drivers match' }}
        columns={[
          { header: 'Driver', primary: true, render: (d) => <Typography sx={{ fontWeight: 650 }}>{d.fullName}</Typography> },
          { header: 'Mobile', primary: true, render: (d) => d.phoneNumber },
          { header: 'Owner', render: (d) => d.ownerName ?? 'Independent' },
          { header: 'Licence till', render: (d) => <ExpiryCell date={d.licenseExpiryDate} /> },
          { header: 'Now', render: (d) => <StatusChip status={availabilityStatus[d.availabilityStatusId]} /> },
          { header: 'Verification', primary: true, render: (d) => <StatusChip status={verificationStatus[d.verificationStatusId]} /> },
        ]}
      />
    </>
  );
}

export function DriverDetailPage() {
  const id = Number(useParams().id);
  const { can } = useAuth();
  const query = useQuery({ queryKey: ['driver', id], queryFn: () => partnerApi.driver(id) });
  const decide = useDecision(['driver', id]);

  return (
    <QueryView query={query}>
      {(d) => (
        <>
          <PageHeader
            back={back('/drivers', 'Drivers')}
            title={d.fullName}
            subtitle={<Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}><span>{d.driverNumber}</span><StatusChip status={verificationStatus[d.verificationStatusId]} /></Stack>}
            actions={
              <>
                {can(P.ApproveDrivers) && <VerificationActions statusId={d.verificationStatusId} onDecide={(s, r) => decide(() => partnerApi.verifyDriver(id, s, r), s === 'Verified' ? 'Driver verified' : 'Driver verification rejected')} />}
                {can(P.ManageDrivers) && <Button onClick={() => decide(() => partnerApi.setDriverActive(id, !d.isActive), d.isActive ? 'Driver deactivated' : 'Driver reactivated')}>{d.isActive ? 'Deactivate' : 'Reactivate'}</Button>}
              </>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Details">
                <DetailList
                  items={[
                    ['Mobile', d.phoneNumber],
                    ['E-mail', d.email],
                    ['Owner', d.ownerId ? <Button component={RouterLink} to={`/owners/${d.ownerId}`} sx={{ p: 0, minWidth: 0 }}>{d.ownerName}</Button> : 'Independent'],
                    ['Date of birth', formatDateOnly(d.dateOfBirth)],
                    ['Licence', d.licenseNumber],
                    ['Class', d.licenseClass],
                    ['Licence valid till', formatDateOnly(d.licenseExpiryDate)],
                    ['Issued by', d.licenseIssuingAuthority],
                    ['Availability', <StatusChip status={availabilityStatus[d.availabilityStatusId]} />],
                    ['Joined', formatDateTime(d.createdDateUtc)],
                  ]}
                />
                {d.verificationRemarks && <Alert severity="info" sx={{ mt: 2 }}>{d.verificationRemarks}</Alert>}
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Documents">
                <DocumentReview entity="Driver" entityId={id} canVerify={can(P.ApproveDrivers)} />
              </Section>
            </Grid>
          </Grid>
        </>
      )}
    </QueryView>
  );
}
