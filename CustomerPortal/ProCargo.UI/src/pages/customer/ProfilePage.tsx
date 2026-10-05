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
  IconButton,
  MenuItem,
  Stack,
  Typography,
} from '@mui/material';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import EditOutlined from '@mui/icons-material/EditOutlined';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { customerApi } from '../../api/endpoints';
import type { Customer, CustomerAddress } from '../../api/types';
import { toApiError } from '../../api/client';
import { applyApiErrors, ConfirmDialog, FormField, useNotify } from '../../components/Forms';
import { EmptyState, PageHeader, QueryView, Section } from '../../components/Layout';
import { CityField } from '../../components/ReferenceFields';
import * as v from '../../lib/validation';

const profileSchema = z
  .object({
    customerType: z.enum(['Individual', 'Business']),
    fullName: v.personName,
    companyName: v.safeText(200).optional(),
    phoneNumber: v.phone,
    gstNumber: v.gst,
  })
  .refine((d) => d.customerType === 'Individual' || !!d.companyName, { path: ['companyName'], message: 'Enter the company name' });

export function CustomerProfilePage() {
  const query = useQuery({ queryKey: ['customer', 'me'], queryFn: customerApi.me });
  return (
    <>
      <PageHeader title="Profile & addresses" />
      <QueryView query={query}>
        {(customer) => (
          <>
            <ProfileForm customer={customer} />
            <Addresses customerId={customer.customerId} />
          </>
        )}
      </QueryView>
    </>
  );
}

function ProfileForm({ customer }: { customer: Customer }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const { control, handleSubmit, watch, setError: setFieldError, formState } = useForm<z.infer<typeof profileSchema>>({
    resolver: zodResolver(profileSchema),
    defaultValues: {
      customerType: customer.customerTypeId === 2 ? 'Business' : 'Individual',
      fullName: customer.fullName,
      companyName: customer.companyName ?? '',
      phoneNumber: customer.phoneNumber,
      gstNumber: customer.gstNumber ?? '',
    },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      await customerApi.update(customer.customerId, {
        ...d,
        companyName: d.customerType === 'Business' ? d.companyName : null,
        gstNumber: d.gstNumber || null,
        rowVersion: customer.rowVersion,
      });
      notify('Profile saved');
      queryClient.invalidateQueries({ queryKey: ['customer', 'me'] });
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Section title="Your details">
      <Box component="form" onSubmit={submit} noValidate>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          Customer number {customer.customerNumber}. Signed in as {customer.email}.
        </Typography>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <FormField control={control} name="customerType" label="Booking as" select>
              <MenuItem value="Individual">An individual</MenuItem>
              <MenuItem value="Business">A business</MenuItem>
            </FormField>
          </Grid>
          <Grid size={{ xs: 12, sm: 8 }}>
            <FormField control={control} name="fullName" label="Full name" />
          </Grid>
          {watch('customerType') === 'Business' && (
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="companyName" label="Company name" />
            </Grid>
          )}
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="gstNumber" label="GSTIN (optional)" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="phoneNumber" label="Mobile" type="tel" />
          </Grid>
        </Grid>
        <Button type="submit" variant="contained" sx={{ mt: 2.5 }} disabled={formState.isSubmitting}>
          Save details
        </Button>
      </Box>
    </Section>
  );
}

const addressSchema = z.object({
  label: v.safeText(50).pipe(z.string().min(2, 'Name this address, e.g. Warehouse')),
  addressLine1: v.safeText(250).pipe(z.string().min(5, 'Enter the street address')),
  addressLine2: v.safeText(250).optional(),
  landmark: v.safeText(150).optional(),
  cityId: z.number().min(1, 'Choose the city'),
  pincode: v.pincode,
  isDefault: z.boolean(),
});
type AddressForm = z.infer<typeof addressSchema>;

function Addresses({ customerId }: { customerId: number }) {
  const query = useQuery({ queryKey: ['customer', 'addresses'], queryFn: () => customerApi.addresses(customerId) });
  const [editing, setEditing] = useState<CustomerAddress | 'new' | null>(null);
  const [deleting, setDeleting] = useState<CustomerAddress | null>(null);
  const queryClient = useQueryClient();
  const notify = useNotify();

  return (
    <Section title="Saved addresses" action={<Button onClick={() => setEditing('new')}>Add address</Button>}>
      <QueryView query={query}>
        {(addresses) =>
          addresses.length === 0 ? (
            <EmptyState title="No saved addresses">Save your warehouse or shop to fill bookings in one tap.</EmptyState>
          ) : (
            <Grid container spacing={2}>
              {addresses.map((a) => (
                <Grid key={a.customerAddressId} size={{ xs: 12, sm: 6 }}>
                  <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1.5, p: 2, height: '100%' }}>
                    <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
                      <Typography sx={{ fontWeight: 700 }}>
                        {a.label}
                        {a.isDefault ? ' (default)' : ''}
                      </Typography>
                      <Box>
                        <IconButton size="small" aria-label={`Edit ${a.label}`} onClick={() => setEditing(a)}>
                          <EditOutlined fontSize="small" />
                        </IconButton>
                        <IconButton size="small" aria-label={`Delete ${a.label}`} onClick={() => setDeleting(a)}>
                          <DeleteOutline fontSize="small" />
                        </IconButton>
                      </Box>
                    </Stack>
                    <Typography variant="body2">{a.addressLine1}</Typography>
                    {a.addressLine2 && <Typography variant="body2">{a.addressLine2}</Typography>}
                    <Typography variant="body2" color="text.secondary">
                      {a.cityName}, {a.stateName} {a.pincode}
                    </Typography>
                  </Box>
                </Grid>
              ))}
            </Grid>
          )
        }
      </QueryView>
      {editing && (
        <AddressDialog
          customerId={customerId}
          address={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            queryClient.invalidateQueries({ queryKey: ['customer', 'addresses'] });
          }}
        />
      )}
      <ConfirmDialog
        open={!!deleting}
        title={`Delete "${deleting?.label}"?`}
        body="Bookings already made with this address are not affected."
        confirmLabel="Delete address"
        destructive
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          try {
            await customerApi.deleteAddress(customerId, deleting!.customerAddressId);
            notify('Address deleted');
            queryClient.invalidateQueries({ queryKey: ['customer', 'addresses'] });
          } catch (e) {
            notify(toApiError(e).message, 'error');
          } finally {
            setDeleting(null);
          }
        }}
      />
    </Section>
  );
}

function AddressDialog({ customerId, address, onClose, onSaved }: { customerId: number; address: CustomerAddress | null; onClose: () => void; onSaved: () => void }) {
  const notify = useNotify();
  const [error, setError] = useState('');
  const { control, handleSubmit, setError: setFieldError, formState } = useForm<AddressForm>({
    resolver: zodResolver(addressSchema),
    defaultValues: {
      label: address?.label ?? '',
      addressLine1: address?.addressLine1 ?? '',
      addressLine2: address?.addressLine2 ?? '',
      landmark: address?.landmark ?? '',
      cityId: address?.cityId ?? 0,
      pincode: address?.pincode ?? '',
      isDefault: address?.isDefault ?? false,
    },
  });

  const submit = handleSubmit(async (d) => {
    setError('');
    try {
      await customerApi.saveAddress(customerId, address?.customerAddressId ?? null, d);
      notify(address ? 'Address updated' : 'Address saved');
      onSaved();
    } catch (e) {
      setError(applyApiErrors(e, setFieldError));
    }
  });

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={submit} noValidate>
        <DialogTitle>{address ? 'Edit address' : 'Add an address'}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid size={12}>
              <FormField control={control} name="label" label="Name" placeholder="Warehouse, Shop, Factory gate 2" />
            </Grid>
            <Grid size={12}>
              <FormField control={control} name="addressLine1" label="Building, street, area" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="addressLine2" label="More detail (optional)" />
            </Grid>
            <Grid size={{ xs: 12, sm: 6 }}>
              <FormField control={control} name="landmark" label="Landmark (optional)" />
            </Grid>
            <Grid size={{ xs: 12, sm: 7 }}>
              <CityField control={control} name="cityId" label="City" />
            </Grid>
            <Grid size={{ xs: 12, sm: 5 }}>
              <FormField control={control} name="pincode" label="PIN code" inputMode="numeric" />
            </Grid>
            <Grid size={12}>
              <Controller
                control={control}
                name="isDefault"
                render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />} label="Use as my default pickup address" />}
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={onClose}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={formState.isSubmitting}>
            Save address
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
