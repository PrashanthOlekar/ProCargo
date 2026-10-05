import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Grid,
  MenuItem,
  Stack,
  Typography,
} from '@mui/material';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { ownerApi } from '../../api/endpoints';
import type { Owner, OwnerProfile } from '../../api/types';
import { toApiError } from '../../api/client';
import { DocumentsPanel } from '../../components/Documents';
import { applyApiErrors, ConfirmDialog, FormField, useNotify } from '../../components/Forms';
import { EmptyState, PageHeader, QueryView, Section } from '../../components/Layout';
import { StatusChip } from '../../components/PlateTag';
import { CityField } from '../../components/ReferenceFields';
import { verificationStatus } from '../../lib/statuses';
import * as v from '../../lib/validation';

export function OwnerProfilePage() {
  const query = useQuery({ queryKey: ['owner', 'me'], queryFn: ownerApi.me });
  return (
    <>
      <PageHeader title="Business profile" subtitle="Verified owners receive trips and payouts. Keep these details current." />
      <QueryView query={query}>
        {(profile) => (
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <OwnerDetails owner={profile.owner} />
              <BusinessDetails owner={profile.owner} />
              <BankAccounts profile={profile} />
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="Verification" action={<StatusChip status={verificationStatus[profile.owner.verificationStatusId]} />}>
                {profile.owner.verificationStatusId === 4 && profile.owner.verificationRemarks && (
                  <Alert severity="error" sx={{ mb: 2 }}>
                    {profile.owner.verificationRemarks}
                  </Alert>
                )}
                <DocumentsPanel entity="Owner" entityId={profile.owner.ownerId} />
              </Section>
              <Addresses profile={profile} />
            </Grid>
          </Grid>
        )}
      </QueryView>
    </>
  );
}

const ownerSchema = z.object({
  ownerType: z.enum(['Individual', 'FleetBusiness']),
  fullName: v.personName,
  phoneNumber: v.phone,
  panNumber: z
    .string()
    .trim()
    .toUpperCase()
    .optional()
    .or(z.literal(''))
    .refine((p) => !p || /^[A-Z]{5}\d{4}[A-Z]$/.test(p), { message: 'Enter a valid 10-character PAN' }),
});

function OwnerDetails({ owner }: { owner: Owner }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof ownerSchema>>({
    resolver: zodResolver(ownerSchema),
    defaultValues: { ownerType: owner.ownerTypeId === 2 ? 'FleetBusiness' : 'Individual', fullName: owner.fullName, phoneNumber: owner.phoneNumber, panNumber: '' },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      await ownerApi.update(owner.ownerId, { ...d, panNumber: d.panNumber || null, rowVersion: owner.rowVersion });
      notify('Details saved');
      queryClient.invalidateQueries({ queryKey: ['owner', 'me'] });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Section title="Owner">
      <Box component="form" onSubmit={submit} noValidate>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {owner.ownerNumber}, {owner.email}
          {owner.panLast4 ? `. PAN ending ${owner.panLast4} on file.` : ''}
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="fullName" label="Full name" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="ownerType" label="Ownership" select>
              <MenuItem value="Individual">Individual owner</MenuItem>
              <MenuItem value="FleetBusiness">Fleet business</MenuItem>
            </FormField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="phoneNumber" label="Mobile" type="tel" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="panNumber" label={owner.panLast4 ? 'Replace PAN (optional)' : 'PAN'} helperText="Stored encrypted; only the last four characters are shown" />
          </Grid>
        </Grid>
        <Button type="submit" variant="contained" sx={{ mt: 2.5 }} disabled={formState.isSubmitting}>
          Save
        </Button>
      </Box>
    </Section>
  );
}

const businessSchema = z.object({
  businessName: v.safeText(200).pipe(z.string().min(2, 'Enter the business name')),
  gstNumber: v.gst,
  registrationNumber: v.safeText(50).optional(),
  fleetSize: z.coerce.number().int().min(1).max(10000).optional(),
});

function BusinessDetails({ owner }: { owner: Owner }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.input<typeof businessSchema>, unknown, z.output<typeof businessSchema>>({
    resolver: zodResolver(businessSchema),
    defaultValues: { businessName: owner.businessName ?? owner.fullName, gstNumber: owner.gstNumber ?? '', registrationNumber: owner.registrationNumber ?? '', fleetSize: owner.fleetSize ?? 1 },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      await ownerApi.saveBusiness(owner.ownerId, { ...d, gstNumber: d.gstNumber || null, registrationNumber: d.registrationNumber || null });
      notify('Business details saved');
      queryClient.invalidateQueries({ queryKey: ['owner', 'me'] });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Section title="Business">
      <Box component="form" onSubmit={submit} noValidate>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="businessName" label="Business or transport name" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="gstNumber" label="GSTIN (optional)" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="registrationNumber" label="Registration number (optional)" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="fleetSize" label="Number of trucks" type="number" inputMode="numeric" />
          </Grid>
        </Grid>
        <Button type="submit" variant="contained" sx={{ mt: 2.5 }} disabled={formState.isSubmitting}>
          Save business details
        </Button>
      </Box>
    </Section>
  );
}

const bankSchema = z.object({
  accountHolderName: v.personName,
  bankName: v.safeText(150).pipe(z.string().min(2, 'Enter the bank name')),
  accountNumber: v.accountNumber,
  confirmAccountNumber: z.string(),
  ifscCode: v.ifsc,
  isPrimary: z.boolean(),
}).refine((d) => d.accountNumber === d.confirmAccountNumber, { path: ['confirmAccountNumber'], message: 'The account numbers do not match' });

function BankAccounts({ profile }: { profile: OwnerProfile }) {
  const [adding, setAdding] = useState(false);
  const [removing, setRemoving] = useState<number | null>(null);
  const queryClient = useQueryClient();
  const notify = useNotify();
  const active = profile.bankAccounts.filter((b) => b.isActive);

  return (
    <Section title="Payout bank account" action={<Button onClick={() => setAdding(true)}>Add account</Button>}>
      {active.length === 0 ? (
        <EmptyState title="No bank account yet">Add the account your trip payouts should go to. A cancelled cheque helps us verify it quickly.</EmptyState>
      ) : (
        <Stack spacing={1.25}>
          {active.map((b) => (
            <Stack key={b.ownerBankAccountId} direction={{ xs: 'column', sm: 'row' }} sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 1.5, justifyContent: 'space-between', gap: 1 }}>
              <Box>
                <Typography sx={{ fontWeight: 650 }}>
                  {b.bankName} ••••{b.accountNumberLast4}
                  {b.isPrimary ? ' (primary)' : ''}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {b.accountHolderName}, IFSC {b.ifscCode}
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <StatusChip status={verificationStatus[b.verificationStatusId]} />
                <Button size="small" color="error" onClick={() => setRemoving(b.ownerBankAccountId)}>
                  Remove
                </Button>
              </Stack>
            </Stack>
          ))}
        </Stack>
      )}
      {adding && <BankDialog ownerId={profile.owner.ownerId} onClose={() => setAdding(false)} onSaved={() => { setAdding(false); queryClient.invalidateQueries({ queryKey: ['owner', 'me'] }); }} />}
      <ConfirmDialog
        open={removing != null}
        title="Remove this bank account?"
        body="Pending payouts will go to your remaining primary account."
        confirmLabel="Remove account"
        destructive
        onClose={() => setRemoving(null)}
        onConfirm={async () => {
          try {
            await ownerApi.deactivateBankAccount(profile.owner.ownerId, removing!);
            notify('Bank account removed');
            queryClient.invalidateQueries({ queryKey: ['owner', 'me'] });
          } catch (e) {
            notify(toApiError(e).message, 'error');
          } finally {
            setRemoving(null);
          }
        }}
      />
    </Section>
  );
}

function BankDialog({ ownerId, onClose, onSaved }: { ownerId: number; onClose: () => void; onSaved: () => void }) {
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<z.infer<typeof bankSchema>>({
    resolver: zodResolver(bankSchema),
    defaultValues: { accountHolderName: '', bankName: '', accountNumber: '', confirmAccountNumber: '', ifscCode: '', isPrimary: true },
  });

  const submit = handleSubmit(async ({ confirmAccountNumber: _confirm, ...d }) => {
    setError('');
    try {
      await ownerApi.addBankAccount(ownerId, d);
      notify('Bank account added. We verify it before the next payout.');
      onSaved();
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>Add a payout account</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid size={12}>
              <FormField control={control} name="accountHolderName" label="Account holder name (as in bank records)" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="bankName" label="Bank" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="ifscCode" label="IFSC" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="accountNumber" label="Account number" inputMode="numeric" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="confirmAccountNumber" label="Repeat account number" inputMode="numeric" />
            </Grid>
            <Grid size={12}>
              <Controller control={control} name="isPrimary" render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />} label="Send payouts to this account" />} />
            </Grid>
          </Grid>
          <Typography variant="body2" color="text.secondary">
            The account number is encrypted before it is stored. Only the last four digits are ever shown again.
          </Typography>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            Add account
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}

const addressSchema = z.object({
  addressLine1: v.safeText(250).pipe(z.string().min(5, 'Enter the street address')),
  addressLine2: v.safeText(250).optional(),
  landmark: v.safeText(150).optional(),
  cityId: z.number().min(1, 'Choose the city'),
  pincode: v.pincode,
});

function Addresses({ profile }: { profile: OwnerProfile }) {
  const [adding, setAdding] = useState(false);
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, reset, setError: setFieldError, formState } = useForm<z.infer<typeof addressSchema>>({
    resolver: zodResolver(addressSchema),
    defaultValues: { addressLine1: '', addressLine2: '', landmark: '', cityId: 0, pincode: '' },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      await ownerApi.addAddress(profile.owner.ownerId, { ...d, isPrimary: profile.addresses.length === 0 });
      notify('Address added');
      reset();
      setAdding(false);
      queryClient.invalidateQueries({ queryKey: ['owner', 'me'] });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Section title="Address" action={!adding ? <Button onClick={() => setAdding(true)}>Add address</Button> : undefined}>
      {profile.addresses.map((a) => (
        <Box key={a.ownerAddressId} sx={{ mb: 1.5 }}>
          <Typography sx={{ fontWeight: 600 }}>
            {a.addressLine1}
            {a.isPrimary ? ' (primary)' : ''}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {a.cityName}, {a.stateName} {a.pincode}
          </Typography>
        </Box>
      ))}
      {profile.addresses.length === 0 && !adding && <Typography color="text.secondary">Add your office or yard address.</Typography>}
      {adding && (
        <Box component="form" onSubmit={submit} noValidate>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2}>
            <Grid size={12}>
              <FormField control={control} name="addressLine1" label="Building, street, area" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="landmark" label="Landmark (optional)" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="pincode" label="PIN code" inputMode="numeric" />
            </Grid>
            <Grid size={12}>
              <CityField control={control} name="cityId" label="City" />
            </Grid>
          </Grid>
          <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
            <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
              Save address
            </Button>
            <Button onClick={() => setAdding(false)}>Cancel</Button>
          </Stack>
        </Box>
      )}
    </Section>
  );
}
