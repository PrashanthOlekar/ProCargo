import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Divider,
  FormControlLabel,
  Grid,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import DeleteOutline from '@mui/icons-material/DeleteOutline';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useFieldArray, useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { bookingApi, customerApi } from '../../api/endpoints';
import type { BookingDetailsResponse, BookingInput, CustomerAddress } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { applyApiErrors, FormField, useNotify } from '../../components/Forms';
import { PageHeader, QueryView, Section } from '../../components/Layout';
import { CityField, useReference } from '../../components/ReferenceFields';
import { formatKg, fromLocalInput, toLocalInput } from '../../lib/format';
import * as v from '../../lib/validation';

const address = z.object({
  addressLine1: v.safeText(250).pipe(z.string().min(5, 'Enter the street address')),
  addressLine2: v.safeText(250).optional(),
  landmark: v.safeText(150).optional(),
  cityId: z.number().min(1, 'Choose the city'),
  pincode: v.pincode,
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
});

const contact = z.object({
  contactName: v.personName,
  phoneNumber: v.phone,
  alternatePhoneNumber: v.optionalPhone,
});

const item = z.object({
  description: v.safeText(200).pipe(z.string().min(2, 'Describe the item')),
  quantity: z.coerce.number().int().min(1, 'At least 1').max(100000),
  weightKg: z.coerce.number().positive('Enter the weight').max(100000),
  isFragile: z.boolean(),
});

const bookingSchema = z.object({
  vehicleTypeId: z.coerce.number().min(1, 'Choose a vehicle'),
  goodsTypeId: z.coerce.number().min(1, 'Choose the type of goods'),
  goodsDescription: v.safeText(500).pipe(z.string().min(3, 'Describe the goods')),
  pickupLocal: z.string().min(1, 'Choose the pickup date and time'),
  specialInstructions: v.safeText(1000).optional(),
  pickupAddress: address,
  deliveryAddress: address,
  pickupContact: contact,
  deliveryContact: contact,
  items: z.array(item).min(1, 'Add at least one item').max(50),
});

type BookingForm = z.infer<typeof bookingSchema>;

const emptyAddress = { addressLine1: '', addressLine2: '', landmark: '', cityId: 0, pincode: '', latitude: null, longitude: null };

function defaultPickup() {
  const d = new Date(Date.now() + 26 * 3600_000);
  d.setUTCMinutes(0, 0, 0);
  return toLocalInput(d.toISOString());
}

function fromDetails(data: BookingDetailsResponse): BookingForm {
  const b = data.booking;
  return {
    vehicleTypeId: b.vehicleTypeId,
    goodsTypeId: b.goodsTypeId,
    goodsDescription: b.goodsDescription,
    pickupLocal: toLocalInput(b.requestedPickupDateUtc),
    specialInstructions: b.specialInstructions ?? '',
    pickupAddress: {
      addressLine1: b.pickupAddressLine1,
      addressLine2: b.pickupAddressLine2 ?? '',
      landmark: b.pickupLandmark ?? '',
      cityId: b.pickupCityId,
      pincode: b.pickupPincode,
      latitude: b.pickupLatitude,
      longitude: b.pickupLongitude,
    },
    deliveryAddress: {
      addressLine1: b.deliveryAddressLine1,
      addressLine2: b.deliveryAddressLine2 ?? '',
      landmark: b.deliveryLandmark ?? '',
      cityId: b.deliveryCityId,
      pincode: b.deliveryPincode,
      latitude: b.deliveryLatitude,
      longitude: b.deliveryLongitude,
    },
    pickupContact: { contactName: b.pickupContactName, phoneNumber: b.pickupContactPhone, alternatePhoneNumber: b.pickupContactAltPhone ?? '' },
    deliveryContact: { contactName: b.deliveryContactName, phoneNumber: b.deliveryContactPhone, alternatePhoneNumber: b.deliveryContactAltPhone ?? '' },
    items: data.items.map((i) => ({ description: i.description, quantity: i.quantity, weightKg: i.weightKg, isFragile: i.isFragile })),
  };
}

export function NewBookingPage() {
  return <BookingEditor />;
}

export function EditBookingPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['booking', id], queryFn: () => bookingApi.get(id) });
  return <QueryView query={query}>{(data) => <BookingEditor existing={data} />}</QueryView>;
}

function BookingEditor({ existing }: { existing?: BookingDetailsResponse }) {
  const { user } = useAuth();
  const reference = useReference();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [error, setError] = useState('');
  const addresses = useQuery({
    queryKey: ['customer', 'addresses'],
    queryFn: () => customerApi.addresses(user!.customerId!),
    enabled: !!user?.customerId,
  });

  const { control, handleSubmit, setValue, watch, setError: setFieldError, formState } = useForm<BookingForm>({
    resolver: zodResolver(bookingSchema),
    defaultValues: existing
      ? fromDetails(existing)
      : {
          vehicleTypeId: 0,
          goodsTypeId: 0,
          goodsDescription: '',
          pickupLocal: defaultPickup(),
          specialInstructions: '',
          pickupAddress: emptyAddress,
          deliveryAddress: emptyAddress,
          pickupContact: { contactName: user?.fullName ?? '', phoneNumber: user?.phoneNumber ?? '', alternatePhoneNumber: '' },
          deliveryContact: { contactName: '', phoneNumber: '', alternatePhoneNumber: '' },
          items: [{ description: '', quantity: 1, weightKg: 0, isFragile: false }],
        },
  });
  const items = useFieldArray({ control, name: 'items' });

  const vehicleTypes = (reference.data?.vehicleTypes ?? []).filter((t) => t.isActive);
  const goodsTypes = (reference.data?.goodsTypes ?? []).filter((t) => t.isActive);
  const totalWeight = watch('items').reduce((sum, i) => sum + (Number(i.weightKg) || 0), 0);
  const vehicle = vehicleTypes.find((t) => t.vehicleTypeId === Number(watch('vehicleTypeId')));
  const overweight = vehicle && totalWeight > vehicle.capacityKg;

  const useSaved = (target: 'pickupAddress' | 'deliveryAddress', a: CustomerAddress) =>
    setValue(target, {
      addressLine1: a.addressLine1,
      addressLine2: a.addressLine2 ?? '',
      landmark: a.landmark ?? '',
      cityId: a.cityId,
      pincode: a.pincode,
      latitude: a.latitude,
      longitude: a.longitude,
    }, { shouldValidate: true });

  const save = (submit: boolean) =>
    handleSubmit(async (f) => {
      setError('');
      const body: BookingInput = {
        vehicleTypeId: f.vehicleTypeId,
        goodsTypeId: f.goodsTypeId,
        goodsDescription: f.goodsDescription,
        requestedPickupDateUtc: fromLocalInput(f.pickupLocal),
        specialInstructions: f.specialInstructions || null,
        pickupAddress: f.pickupAddress,
        deliveryAddress: f.deliveryAddress,
        pickupContact: f.pickupContact,
        deliveryContact: f.deliveryContact,
        items: f.items,
      };
      try {
        if (existing) {
          await bookingApi.update(existing.booking.bookingId, { ...body, rowVersion: existing.booking.rowVersion });
          if (submit && existing.booking.bookingStatusId === 1) await bookingApi.submit(existing.booking.bookingId);
          await queryClient.invalidateQueries({ queryKey: ['booking', existing.booking.bookingId] });
          notify('Booking updated');
          navigate(`/customer/bookings/${existing.booking.bookingId}`);
        } else {
          const created = await bookingApi.create({ ...body, submit });
          notify(submit ? `Booking ${created.number} sent for a quotation` : `Draft ${created.number} saved`);
          navigate(`/customer/bookings/${created.id}`);
        }
      } catch (e) {
        setError(applyApiErrors(e, setFieldError) || 'Please correct the highlighted fields.');
        window.scrollTo({ top: 0, behavior: 'smooth' });
      }
    });

  const addressBlock = (prefix: 'pickupAddress' | 'deliveryAddress', contactPrefix: 'pickupContact' | 'deliveryContact', title: string, contactHint: string) => (
    <Section
      title={title}
      action={
        (addresses.data?.length ?? 0) > 0 ? (
          <TextField select size="small" label="Use a saved address" value="" sx={{ minWidth: 220 }} onChange={(e) => {
            const a = addresses.data?.find((x) => x.customerAddressId === Number(e.target.value));
            if (a) useSaved(prefix, a);
          }}>
            {addresses.data?.map((a) => (
              <MenuItem key={a.customerAddressId} value={a.customerAddressId}>
                {a.label} — {a.cityName}
              </MenuItem>
            ))}
          </TextField>
        ) : undefined
      }
    >
      <Grid container spacing={2}>
        <Grid size={12}>
          <FormField control={control} name={`${prefix}.addressLine1`} label="Building, street, area" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormField control={control} name={`${prefix}.addressLine2`} label="More detail (optional)" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <FormField control={control} name={`${prefix}.landmark`} label="Landmark (optional)" />
        </Grid>
        <Grid size={{ xs: 12, sm: 7 }}>
          <CityField control={control} name={`${prefix}.cityId`} label="City" />
        </Grid>
        <Grid size={{ xs: 12, sm: 5 }}>
          <FormField control={control} name={`${prefix}.pincode`} label="PIN code" inputMode="numeric" />
        </Grid>
      </Grid>
      <Divider sx={{ my: 2.5 }} />
      <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
        {contactHint}
      </Typography>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 4 }}>
          <FormField control={control} name={`${contactPrefix}.contactName`} label="Contact name" />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <FormField control={control} name={`${contactPrefix}.phoneNumber`} label="Mobile" type="tel" inputMode="tel" />
        </Grid>
        <Grid size={{ xs: 12, sm: 4 }}>
          <FormField control={control} name={`${contactPrefix}.alternatePhoneNumber`} label="Second number (optional)" type="tel" inputMode="tel" />
        </Grid>
      </Grid>
    </Section>
  );

  return (
    <Box component="form" noValidate onSubmit={save(true)}>
      <PageHeader
        back={
          <Button component={RouterLink} to={existing ? `/customer/bookings/${existing.booking.bookingId}` : '/customer/bookings'} startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
            {existing ? 'Back to booking' : 'My bookings'}
          </Button>
        }
        title={existing ? `Edit booking ${existing.booking.bookingNumber}` : 'Book a lorry'}
        subtitle="We'll send you a fixed-price quotation. Nothing is charged until you accept it and the goods are delivered."
      />
      {error && (
        <Alert severity="error" sx={{ mb: 2.5 }}>
          {error}
        </Alert>
      )}

      {addressBlock('pickupAddress', 'pickupContact', 'Pickup', 'Who hands over the goods? The pickup OTP is sent to this number.')}
      {addressBlock('deliveryAddress', 'deliveryContact', 'Delivery', 'Who receives the goods? The delivery OTP is sent to this number.')}

      <Section title="Goods and vehicle">
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="goodsTypeId" label="Type of goods" select>
              <MenuItem value={0} disabled>
                Choose
              </MenuItem>
              {goodsTypes.map((g) => (
                <MenuItem key={g.goodsTypeId} value={g.goodsTypeId}>
                  {g.name}
                  {g.requiresSpecialHandling ? ' (special handling)' : ''}
                </MenuItem>
              ))}
            </FormField>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="pickupLocal" label="Pickup date and time (IST)" type="datetime-local" />
          </Grid>
          <Grid size={12}>
            <FormField control={control} name="goodsDescription" label="What are you sending?" helperText="For example: 280 cartons of packaged biscuits" />
          </Grid>
        </Grid>

        <Typography variant="h6" component="h3" sx={{ mt: 3, mb: 1.5 }}>
          Items
        </Typography>
        <Stack spacing={1.5}>
          {items.fields.map((field, index) => (
            <Grid container spacing={1.5} key={field.id} sx={{ alignItems: 'flex-start' }}>
              <Grid size={{ xs: 12, sm: 5 }}>
                <FormField control={control} name={`items.${index}.description`} label="Item" />
              </Grid>
              <Grid size={{ xs: 4, sm: 2 }}>
                <FormField control={control} name={`items.${index}.quantity`} label="Qty" type="number" inputMode="numeric" />
              </Grid>
              <Grid size={{ xs: 5, sm: 2.5 }}>
                <FormField control={control} name={`items.${index}.weightKg`} label="Total kg" type="number" inputMode="decimal" />
              </Grid>
              <Grid size={{ xs: 3, sm: 2.5 }} sx={{ display: 'flex', alignItems: 'center' }}>
                <Controller
                  control={control}
                  name={`items.${index}.isFragile`}
                  render={({ field: f }) => <FormControlLabel control={<Checkbox checked={!!f.value} onChange={(e) => f.onChange(e.target.checked)} />} label="Fragile" />}
                />
                {items.fields.length > 1 && (
                  <IconButton aria-label={`Remove item ${index + 1}`} onClick={() => items.remove(index)}>
                    <DeleteOutline />
                  </IconButton>
                )}
              </Grid>
            </Grid>
          ))}
        </Stack>
        <Button onClick={() => items.append({ description: '', quantity: 1, weightKg: 0, isFragile: false })} sx={{ mt: 1 }}>
          Add another item
        </Button>
        {formState.errors.items?.root && <Alert severity="error" sx={{ mt: 1 }}>{formState.errors.items.root.message}</Alert>}

        <Divider sx={{ my: 2.5 }} />
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <FormField control={control} name="vehicleTypeId" label="Vehicle" select helperText={`Total load ${formatKg(totalWeight)}`}>
              <MenuItem value={0} disabled>
                Choose a vehicle
              </MenuItem>
              {vehicleTypes.map((t) => (
                <MenuItem key={t.vehicleTypeId} value={t.vehicleTypeId} disabled={t.capacityKg < totalWeight}>
                  {t.name} — up to {formatKg(t.capacityKg)}
                </MenuItem>
              ))}
            </FormField>
          </Grid>
          <Grid size={12}>
            <FormField control={control} name="specialInstructions" label="Instructions for the driver (optional)" multiline rows={2} />
          </Grid>
        </Grid>
        {overweight && (
          <Alert severity="warning" sx={{ mt: 2 }}>
            {formatKg(totalWeight)} is more than a {vehicle?.name} carries. Choose a bigger vehicle or split the load.
          </Alert>
        )}
      </Section>

      <Stack direction={{ xs: 'column-reverse', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'flex-end' }}>
        {(!existing || existing.booking.bookingStatusId === 1) && (
          <Button onClick={save(false)} disabled={formState.isSubmitting} size="large">
            Save as draft
          </Button>
        )}
        <Button type="submit" variant="contained" size="large" disabled={formState.isSubmitting || !!overweight}>
          {formState.isSubmitting ? 'Sending…' : existing && existing.booking.bookingStatusId !== 1 ? 'Save changes' : 'Request a quotation'}
        </Button>
      </Stack>
    </Box>
  );
}
