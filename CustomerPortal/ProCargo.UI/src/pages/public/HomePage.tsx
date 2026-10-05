import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Container, Divider, Grid, MenuItem, Stack, TextField, Typography, FormControlLabel, Checkbox } from '@mui/material';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { referenceApi } from '../../api/endpoints';
import type { PriceEstimate } from '../../api/types';
import { toApiError } from '../../api/client';
import { useCities, useReference } from '../../components/ReferenceFields';
import { FormField } from '../../components/Forms';
import { PlateTag } from '../../components/PlateTag';
import { formatKg, formatMoney } from '../../lib/format';
import { palette } from '../../theme';

const estimateSchema = z.object({
  vehicleTypeId: z.coerce.number().min(1, 'Choose a vehicle'),
  pickupCityId: z.coerce.number().optional(),
  deliveryCityId: z.coerce.number().optional(),
  distanceKm: z.coerce.number({ invalid_type_error: 'Enter the distance' }).positive('Enter the distance').max(5000),
  weightKg: z.coerce.number({ invalid_type_error: 'Enter the weight' }).min(1, 'Enter the weight').max(100000),
  includeLoading: z.boolean(),
  includeUnloading: z.boolean(),
});
type EstimateForm = z.infer<typeof estimateSchema>;

const steps = [
  ['Tell us the load', 'Pickup and drop address, goods, weight and the day you need the truck.'],
  ['Get a fixed quotation', 'Our operations desk prices the trip — tolls and loading included — usually within the hour.'],
  ['Hand over with an OTP', 'The driver collects only after your pickup contact reads out a one-time code. Delivery works the same way.'],
  ['Track, receive, pay', 'Follow the truck on the map, see photo proof of delivery and pay the GST invoice online.'],
];

export default function HomePage() {
  const reference = useReference();
  const cities = useCities();
  const [estimate, setEstimate] = useState<PriceEstimate | null>(null);
  const [error, setError] = useState('');
  const { control, handleSubmit, register, watch, formState } = useForm<EstimateForm>({
    resolver: zodResolver(estimateSchema),
    defaultValues: { vehicleTypeId: 0, distanceKm: undefined, weightKg: undefined, includeLoading: true, includeUnloading: true },
  });

  const vehicleTypes = (reference.data?.vehicleTypes ?? []).filter((v) => v.isActive);
  const selected = vehicleTypes.find((v) => v.vehicleTypeId === Number(watch('vehicleTypeId')));
  const cityName = (id?: number) => cities.data?.find((c) => c.cityId === id)?.name;

  const onEstimate = handleSubmit(async (values) => {
    setError('');
    try {
      setEstimate(
        await referenceApi.estimate({
          ...values,
          pickupCityId: values.pickupCityId || undefined,
          deliveryCityId: values.deliveryCityId || undefined,
        }),
      );
    } catch (e) {
      setEstimate(null);
      setError(toApiError(e).message);
    }
  });

  return (
    <>
      <Box sx={{ bgcolor: palette.teal, color: '#fff', pt: { xs: 5, md: 8 }, pb: { xs: 6, md: 9 } }}>
        <Container maxWidth="lg">
          <Grid container spacing={{ xs: 4, md: 6 }} sx={{ alignItems: 'flex-start' }}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Typography variant="h1">A lorry for your load, priced before you book.</Typography>
              <Typography sx={{ mt: 2.5, fontSize: '1.15rem', maxWidth: 520, color: '#D6E7E5' }}>
                Tata Aces to 32-foot multi-axle trucks across Karnataka and South India, from verified owners. Pickup and
                delivery are confirmed with a one-time code, so your goods never leave without your say-so.
              </Typography>
              <Stack direction="row" spacing={1.5} sx={{ mt: 4, flexWrap: 'wrap', gap: 1.5 }}>
                <Button component={RouterLink} to="/register" size="large" sx={{ bgcolor: palette.plate, color: palette.asphalt, '&:hover': { bgcolor: '#E3B610' } }}>
                  Book your first lorry
                </Button>
                <Button component={RouterLink} to="/partners" size="large" variant="outlined" sx={{ color: '#fff', borderColor: 'rgba(255,255,255,.5)' }}>
                  I own trucks
                </Button>
              </Stack>
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <Card sx={{ border: 0 }}>
                <CardContent component="form" onSubmit={onEstimate} noValidate sx={{ p: { xs: 2.5, md: 3.5 } }}>
                  <Typography variant="h4" component="h2">
                    Instant price estimate
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mb: 2.5 }}>
                    An indication from today's rate card. Your confirmed price comes in the quotation.
                  </Typography>
                  <Grid container spacing={1.75}>
                    <Grid size={{ xs: 12, sm: 6 }}>
                      <TextField select label="From" defaultValue="" {...register('pickupCityId')}>
                        <MenuItem value="">Any city</MenuItem>
                        {(cities.data ?? []).map((c) => (
                          <MenuItem key={c.cityId} value={c.cityId}>
                            {c.name}
                          </MenuItem>
                        ))}
                      </TextField>
                    </Grid>
                    <Grid size={{ xs: 12, sm: 6 }}>
                      <TextField select label="To" defaultValue="" {...register('deliveryCityId')}>
                        <MenuItem value="">Any city</MenuItem>
                        {(cities.data ?? []).map((c) => (
                          <MenuItem key={c.cityId} value={c.cityId}>
                            {c.name}
                          </MenuItem>
                        ))}
                      </TextField>
                    </Grid>
                    <Grid size={12}>
                      <FormField control={control} name="vehicleTypeId" label="Vehicle" select>
                        <MenuItem value={0} disabled>
                          Choose a vehicle
                        </MenuItem>
                        {vehicleTypes.map((v) => (
                          <MenuItem key={v.vehicleTypeId} value={v.vehicleTypeId}>
                            {v.name} — up to {formatKg(v.capacityKg)}
                          </MenuItem>
                        ))}
                      </FormField>
                    </Grid>
                    <Grid size={{ xs: 6 }}>
                      <FormField control={control} name="distanceKm" label="Distance (km)" type="number" inputMode="decimal" />
                    </Grid>
                    <Grid size={{ xs: 6 }}>
                      <FormField
                        control={control}
                        name="weightKg"
                        label="Weight (kg)"
                        type="number"
                        inputMode="decimal"
                        helperText={selected ? `Max ${formatKg(selected.capacityKg)}` : undefined}
                      />
                    </Grid>
                    <Grid size={12}>
                      <FormControlLabel control={<Checkbox defaultChecked {...register('includeLoading')} />} label="Loading by our crew" />
                      <FormControlLabel control={<Checkbox defaultChecked {...register('includeUnloading')} />} label="Unloading" />
                    </Grid>
                  </Grid>
                  {error && (
                    <Alert severity="warning" sx={{ mt: 2 }}>
                      {error}
                    </Alert>
                  )}
                  <Button type="submit" variant="contained" size="large" fullWidth sx={{ mt: 2 }} disabled={formState.isSubmitting}>
                    {formState.isSubmitting ? 'Calculating…' : 'Show the price'}
                  </Button>

                  {estimate && (
                    <Box sx={{ mt: 3 }} aria-live="polite">
                      <Divider sx={{ mb: 2 }} />
                      <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'baseline', gap: 1 }}>
                        <Typography color="text.secondary">
                          {cityName(watch('pickupCityId')) ?? 'Pickup'} to {cityName(watch('deliveryCityId')) ?? 'drop'}
                        </Typography>
                        <PlateTag size="large">{formatMoney(estimate.totalAmount)}</PlateTag>
                      </Stack>
                      <Box component="table" sx={{ width: '100%', mt: 2, borderCollapse: 'collapse', fontSize: '0.92rem' }}>
                        <tbody>
                          {estimate.lines.map((l, i) => (
                            <tr key={i}>
                              <Box component="td" sx={{ py: 0.4, color: 'text.secondary' }}>
                                {l.description}
                              </Box>
                              <Box component="td" sx={{ py: 0.4, textAlign: 'right' }}>
                                {formatMoney(l.amount)}
                              </Box>
                            </tr>
                          ))}
                          <tr>
                            <Box component="td" sx={{ py: 0.4, color: 'text.secondary' }}>
                              GST {estimate.taxPercent}%
                            </Box>
                            <Box component="td" sx={{ py: 0.4, textAlign: 'right' }}>
                              {formatMoney(estimate.taxAmount)}
                            </Box>
                          </tr>
                        </tbody>
                      </Box>
                      <Button component={RouterLink} to="/customer/bookings/new" fullWidth sx={{ mt: 1.5 }}>
                        Book this trip
                      </Button>
                    </Box>
                  )}
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </Container>
      </Box>

      <Container maxWidth="lg" sx={{ py: { xs: 6, md: 9 } }}>
        <Typography variant="h2" sx={{ maxWidth: 640 }}>
          From enquiry to delivered, in four steps
        </Typography>
        <Grid container spacing={3} component="ol" sx={{ listStyle: 'none', p: 0, mt: 4 }}>
          {steps.map(([title, text], i) => (
            <Grid key={title} size={{ xs: 12, sm: 6, md: 3 }} component="li">
              <Typography sx={{ fontWeight: 800, fontStretch: '75%', fontSize: '2.6rem', color: palette.teal, lineHeight: 1 }}>{i + 1}</Typography>
              <Typography variant="h5" component="h3" sx={{ mt: 1 }}>
                {title}
              </Typography>
              <Typography color="text.secondary" sx={{ mt: 0.75 }}>
                {text}
              </Typography>
            </Grid>
          ))}
        </Grid>
      </Container>

      <Box sx={{ bgcolor: '#fff', borderBlock: 1, borderColor: 'divider', py: { xs: 6, md: 8 } }}>
        <Container maxWidth="lg">
          <Typography variant="h2">The fleet</Typography>
          <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 560 }}>
            Every vehicle is checked for RC, permit, insurance, fitness and PUC before it can take a booking, and again before each
            document expires.
          </Typography>
          <Box sx={{ mt: 4, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: 'repeat(3, 1fr)' }, border: 1, borderColor: 'divider', borderRadius: 1.5, overflow: 'hidden' }}>
            {vehicleTypes.map((v) => (
              <Box key={v.vehicleTypeId} sx={{ p: 2.5, borderRight: 1, borderBottom: 1, borderColor: 'divider', mr: '-1px', mb: '-1px' }}>
                <Typography variant="h5" component="h3">
                  {v.name}
                </Typography>
                <Typography color="text.secondary" variant="body2" sx={{ mt: 0.5 }}>
                  Carries up to {formatKg(v.capacityKg)}
                  {v.lengthFt ? `, ${v.lengthFt} ft body` : ''}
                </Typography>
                {v.description && <Typography variant="body2" sx={{ mt: 1 }}>{v.description}</Typography>}
              </Box>
            ))}
          </Box>
        </Container>
      </Box>

      <Container maxWidth="md" sx={{ py: { xs: 6, md: 9 }, textAlign: 'left' }}>
        <Typography variant="h2">Paperwork included</Typography>
        <Typography sx={{ mt: 2, fontSize: '1.1rem' }} color="text.secondary">
          Every trip ends with a photo proof of delivery signed by your receiver and a GST-compliant invoice for the amount you
          accepted in the quotation — nothing added at the gate. Pay by UPI, card or net banking, or by bank transfer against the
          invoice.
        </Typography>
        <Button component={RouterLink} to="/register" variant="contained" size="large" sx={{ mt: 3 }}>
          Create a business account
        </Button>
      </Container>
    </>
  );
}
