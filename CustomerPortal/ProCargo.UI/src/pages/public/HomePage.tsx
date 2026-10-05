import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Checkbox, Divider, FormControlLabel, Grid, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { referenceApi } from '../../api/endpoints';
import type { PriceEstimate, VehicleType } from '../../api/types';
import { toApiError } from '../../api/client';
import { useCities, useReference } from '../../components/ReferenceFields';
import { FormField } from '../../components/Forms';
import { PlateTag } from '../../components/PlateTag';
import { formatKg, formatMoney } from '../../lib/format';
import { palette } from '../../theme';
import './home.css';

/*
  The public home page is one ProCargo trip: Bengaluru to Hubballi on NH 48.
  Everything it says follows the real workflow (Docs/Workflows): quotation from the operations desk, OTP handover,
  live tracking, photo proof of delivery, GST invoice, owner settlement after the customer pays.
*/

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

/** The route used on the page, and the kilometres left at each step's roadside stone. */
const ROUTE_KM = 412;

const steps: { km: number; title: string; text: string }[] = [
  { km: 412, title: 'Tell us the load', text: 'Pickup and drop address, the goods, the weight and the window you need the truck in.' },
  { km: 380, title: 'Get a fixed quotation', text: 'Our operations desk prices the trip from the rate card, loading and unloading included. The quotation stays valid for 48 hours.' },
  { km: 342, title: 'Accept, and it is confirmed', text: 'Accept the quotation in your account. That price is what your invoice will bill, nothing added at the gate.' },
  { km: 300, title: 'A verified truck is assigned', text: 'Only vehicles and drivers whose RC, permit, insurance, fitness, PUC and licence are verified and unexpired can take your load.' },
  { km: 212, title: 'Pickup with a one-time code', text: 'Your pickup contact gets an OTP by SMS and reads it out to the driver. No code, no departure.' },
  { km: 147, title: 'Track it live', text: 'The driver shares location while in transit, so you can follow the truck on the map.' },
  { km: 40, title: 'Delivery code and photo proof', text: 'Your receiver gives the driver the delivery OTP, and the driver uploads photos of the signed delivery receipt.' },
  { km: 0, title: 'GST invoice, pay your way', text: 'The invoice is due in 7 days. Pay by UPI, card or net banking, or by bank transfer against the invoice.' },
];

export default function HomePage() {
  const form = useForm<EstimateForm>({
    resolver: zodResolver(estimateSchema),
    defaultValues: { vehicleTypeId: 0, distanceKm: undefined, weightKg: undefined, includeLoading: true, includeUnloading: true },
  });
  const reference = useReference();
  const vehicleTypes = (reference.data?.vehicleTypes ?? []).filter((v) => v.isActive).sort((a, b) => a.sortOrder - b.sortOrder);

  /** "Price this lorry" in the fleet line-up fills the estimate form and brings it into view. */
  const priceVehicle = (vehicle: VehicleType, weightKg: number) => {
    form.setValue('vehicleTypeId', vehicle.vehicleTypeId, { shouldValidate: true });
    form.setValue('weightKg', Math.min(weightKg, vehicle.capacityKg));
    document.getElementById('estimate')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  };

  return (
    <div className="hp">
      <section className="hp-hero" aria-labelledby="hp-title">
        <div className="hp-wrap hp-hero-grid">
          <div>
            <h1 className="hp-title" id="hp-title">
              <span>A lorry for your load,</span>
              <span>priced before</span>
              <span>you book.</span>
            </h1>
            <p className="hp-lede">
              Tata Aces to 40-foot trailers across Karnataka and South India, from verified owners. Pickup and delivery are
              confirmed with a one-time code, so your goods never leave without your say-so.
            </p>
            <Stack direction="row" sx={{ mt: 4, flexWrap: 'wrap', gap: 1.5 }}>
              <Button
                component={RouterLink}
                to="/register"
                size="large"
                variant="contained"
                sx={{ bgcolor: '#fff', color: palette.asphalt, '&:hover': { bgcolor: palette.tealTint } }}
              >
                Book your first lorry
              </Button>
              <Button component={RouterLink} to="/partners" size="large" variant="outlined" sx={{ color: '#fff', borderColor: 'rgba(255,255,255,.5)' }}>
                I own trucks
              </Button>
            </Stack>
            <ul className="hp-promises">
              <li>Verified owners and drivers</li>
              <li>OTP at pickup and delivery</li>
              <li>GST invoice on every trip</li>
            </ul>
          </div>
          <EstimateCard form={form} vehicleTypes={vehicleTypes} />
        </div>

        <div className="hp-road" aria-hidden="true">
          <div className="hp-stone">
            <span>Hubballi</span>
            <b>{ROUTE_KM}</b>
          </div>
          <LorrySide className="hp-lorry" />
        </div>
      </section>

      <FleetSection vehicleTypes={vehicleTypes} onPrice={priceVehicle} />
      <JourneySection />
      <OwnersSection />
      <PaperworkSection />
    </div>
  );
}

// ---------------- estimate ----------------

function EstimateCard({ form, vehicleTypes }: { form: UseFormReturn<EstimateForm>; vehicleTypes: VehicleType[] }) {
  const cities = useCities();
  const [estimate, setEstimate] = useState<PriceEstimate | null>(null);
  const [error, setError] = useState('');
  const { control, handleSubmit, register, watch, formState } = form;

  const selected = vehicleTypes.find((v) => v.vehicleTypeId === Number(watch('vehicleTypeId')));
  const cityName = (id?: number) => cities.data?.find((c) => c.cityId === Number(id))?.name;
  const route = `${cityName(watch('pickupCityId')) ?? 'Pickup'} to ${cityName(watch('deliveryCityId')) ?? 'drop'}`;

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
    <Card id="estimate" sx={{ border: 0, borderRadius: 2, boxShadow: '0 24px 60px rgba(0,0,0,.35)', color: 'text.primary' }}>
      <div className="hp-sign">
        <h2 className="hp-sign-label">Instant price estimate</h2>
        <p className="hp-sign-route" aria-live="polite">
          {route}
        </p>
      </div>
      <CardContent component="form" onSubmit={onEstimate} noValidate sx={{ p: { xs: 2.5, md: 3 }, pt: { xs: 1.5, md: 1.5 } }}>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
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
              <Typography color="text.secondary">{route}</Typography>
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
  );
}

// ---------------- fleet line-up ----------------

const SLIDER_MIN_KG = 100;
const SLIDER_MAX_KG = 25000;
const sliderToKg = (value: number) => {
  const kg = SLIDER_MIN_KG * (SLIDER_MAX_KG / SLIDER_MIN_KG) ** (value / 1000);
  return kg < 1000 ? Math.round(kg / 10) * 10 : Math.round(kg / 100) * 100;
};
const kgToSlider = (kg: number) => Math.round((Math.log(kg / SLIDER_MIN_KG) / Math.log(SLIDER_MAX_KG / SLIDER_MIN_KG)) * 1000);

function FleetSection({ vehicleTypes, onPrice }: { vehicleTypes: VehicleType[]; onPrice: (vehicle: VehicleType, weightKg: number) => void }) {
  const [slider, setSlider] = useState(() => kgToSlider(3000));
  const load = sliderToKg(slider);
  const longest = Math.max(40, ...vehicleTypes.map((v) => v.lengthFt ?? 0));
  const best = vehicleTypes.filter((v) => v.capacityKg >= load).sort((a, b) => a.capacityKg - b.capacityKg)[0];

  return (
    <section className="hp-section" aria-labelledby="hp-fleet">
      <div className="hp-wrap">
        <div className="hp-head">
          <h2 id="hp-fleet">Which lorry fits your load?</h2>
          <p>
            Every class from a Tata Ace to a 40 ft trailer, drawn to scale. Slide to your load: the lorries that can't carry it
            fade, and the smallest that can is marked. Each vehicle is checked for RC, permit, insurance, fitness and PUC.
          </p>
        </div>

        <div className="hp-meter">
          <label htmlFor="hp-load">Your load</label>
          <input id="hp-load" type="range" min={0} max={1000} value={slider} onChange={(e) => setSlider(Number(e.target.value))} />
          <output htmlFor="hp-load">{formatKg(load)}</output>
        </div>

        <ol className="hp-fleet">
          {vehicleTypes.map((v) => {
            const tooSmall = v.capacityKg < load;
            const width = Math.round(((v.lengthFt ?? 8) / longest) * 210 + 18);
            return (
              <li key={v.vehicleTypeId} className={tooSmall ? 'is-small' : v === best ? 'is-best' : undefined}>
                <div className="hp-scale" aria-hidden="true">
                  <i style={{ width }} />
                </div>
                <div className="hp-fleet-name">
                  <b>
                    {v.name}
                    <span className="hp-best">Best fit</span>
                  </b>
                  <span>
                    {v.description}
                    {v.lengthFt ? ` · ${v.lengthFt} ft body` : ''}
                  </span>
                </div>
                <div className="hp-cap">{formatKg(v.capacityKg)}</div>
                <Button onClick={() => onPrice(v, load)} sx={{ justifySelf: 'start' }}>
                  Price this lorry
                </Button>
              </li>
            );
          })}
        </ol>
      </div>
    </section>
  );
}

// ---------------- the journey: scroll drives the lorry ----------------

function JourneySection() {
  const laneRef = useRef<HTMLDivElement>(null);
  const lorryRef = useRef<HTMLDivElement>(null);
  const stepRefs = useRef<(HTMLLIElement | null)[]>([]);
  const [kmLeft, setKmLeft] = useState(ROUTE_KM);
  const [reached, setReached] = useState(0);

  useEffect(() => {
    let queued = false;
    const update = () => {
      queued = false;
      const lane = laneRef.current;
      const lorry = lorryRef.current;
      if (!lane || !lorry) return;
      const box = lane.getBoundingClientRect();
      const lorryHeight = lorry.offsetHeight;
      const travel = Math.max(1, box.height - lorryHeight);
      // The lorry's nose follows a line 60% down the screen.
      const progress = Math.min(1, Math.max(0, (window.innerHeight * 0.6 - box.top - lorryHeight) / travel));
      lorry.style.transform = `translateY(${progress * travel}px)`;
      setKmLeft(Math.round(ROUTE_KM * (1 - progress)));
      const nose = box.top + progress * travel + lorryHeight;
      setReached(stepRefs.current.filter((step) => step && step.getBoundingClientRect().top <= nose).length);
    };
    const queue = () => {
      if (!queued) {
        queued = true;
        requestAnimationFrame(update);
      }
    };
    update();
    window.addEventListener('scroll', queue, { passive: true });
    window.addEventListener('resize', queue);
    return () => {
      window.removeEventListener('scroll', queue);
      window.removeEventListener('resize', queue);
    };
  }, []);

  const current = steps[Math.max(0, reached - 1)];

  return (
    <section className="hp-section hp-journey" aria-labelledby="hp-journey">
      <div className="hp-wrap">
        <div className="hp-head">
          <h2 id="hp-journey">From enquiry to delivered, in eight stops.</h2>
          <p>One run on NH 48, {ROUTE_KM} km from Bengaluru to Hubballi. Scroll, and the lorry drives it.</p>
        </div>

        <div className="hp-journey-grid">
          <div className="hp-board" aria-hidden="true">
            <div className="hp-board-sign">
              <span>↑ Hubballi</span>
              <b>{kmLeft} km</b>
            </div>
            <p>{current.title}</p>
          </div>

          <div className="hp-lane" ref={laneRef} aria-hidden="true">
            <div className="hp-lane-lorry" ref={lorryRef}>
              <LorryTop />
            </div>
          </div>

          <ol className="hp-steps">
            {steps.map((step, index) => (
              <li
                key={step.title}
                ref={(element) => {
                  stepRefs.current[index] = element;
                }}
                className={index < reached ? 'is-reached' : undefined}
              >
                <div className="hp-stone" aria-hidden="true">
                  <span>Hubballi</span>
                  <b>{step.km}</b>
                </div>
                <div>
                  <h3>{step.title}</h3>
                  <p>{step.text}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>
      </div>
    </section>
  );
}

// ---------------- owners and paperwork ----------------

function OwnersSection() {
  const facts = [
    ['Loads that match your truck', 'Trips that fit your vehicle type and capacity, with no broker calls and no haggling at the loading point.'],
    ['Paid to your bank account', 'Freight before tax, minus a 10% platform commission and TDS where it applies, settled to your verified account after the customer pays.'],
    ['Your drivers, your fleet', 'Add drivers and keep RC, permit, insurance and fitness documents in one place, with reminders before they expire.'],
  ];
  return (
    <section className="hp-section hp-owners" aria-labelledby="hp-owners">
      <div className="hp-wrap hp-split">
        <div>
          <h2 id="hp-owners">Keep your trucks loaded.</h2>
          <p>Join as a truck owner to receive verified bookings, or as a driver to run trips from your phone.</p>
          <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1.5 }}>
            <Button
              component={RouterLink}
              to="/register?as=VehicleOwner"
              size="large"
              variant="contained"
              sx={{ bgcolor: '#fff', color: palette.tealDark, '&:hover': { bgcolor: palette.tealTint } }}
            >
              Register my trucks
            </Button>
            <Button component={RouterLink} to="/register?as=Driver" size="large" variant="outlined" sx={{ color: '#fff', borderColor: 'rgba(255,255,255,.6)' }}>
              Register as a driver
            </Button>
          </Stack>
        </div>
        <dl className="hp-facts">
          {facts.map(([title, text]) => (
            <div key={title}>
              <dt>{title}</dt>
              <dd>{text}</dd>
            </div>
          ))}
        </dl>
      </div>
    </section>
  );
}

function PaperworkSection() {
  const checks = [
    ['Checked before the first trip', 'Owner PAN and bank account, vehicle RC, permit, insurance, fitness and PUC, and the driver’s licence, all verified by our team.'],
    ['Out of the list when papers lapse', 'A vehicle or driver with an expired document can’t be assigned until it is renewed and verified again.'],
    ['Two codes per trip', 'One for pickup, one for delivery, each sent by SMS to your contact and valid for 10 minutes.'],
    ['Photo proof of delivery', 'Photos of the signed receipt are uploaded from the drop point with the receiver’s name.'],
    ['An invoice for the price you accepted', 'Lines copied from the accepted quotation, with GST, and nothing added at the gate.'],
  ];
  return (
    <section className="hp-section" aria-labelledby="hp-paperwork">
      <div className="hp-wrap hp-split">
        <div>
          <h2 id="hp-paperwork">Paperwork included.</h2>
          <Typography color="text.secondary" sx={{ fontSize: '1.08rem', maxWidth: '44ch', mb: 3 }}>
            Every trip ends with photo proof of delivery and a GST invoice. Pay online, or by bank transfer against the invoice.
          </Typography>
          <Button component={RouterLink} to="/register" variant="contained" size="large">
            Create a business account
          </Button>
        </div>
        <ul className="hp-checks">
          {checks.map(([title, text]) => (
            <li key={title}>
              <b>{title}</b>
              {text}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}

// ---------------- drawings ----------------

/** The ProCargo lorry from the side: teal cab, white body, yellow number plate. */
function LorrySide({ className }: { className?: string }) {
  const wheel = (cx: number) => (
    <g className="hp-wheel">
      <circle cx={cx} cy={80} r={13} fill="#1E2328" />
      <circle cx={cx} cy={80} r={5} fill="#D7DCD9" />
      <rect x={cx - 1} y={70} width={2} height={20} fill="#D7DCD9" />
    </g>
  );
  return (
    <svg className={className} viewBox="0 0 220 96" aria-hidden="true">
      <rect x="4" y="10" width="140" height="62" rx="4" fill="#FFFFFF" />
      <rect x="4" y="10" width="140" height="10" rx="4" fill="#0F5F5B" />
      <rect x="4" y="62" width="140" height="10" fill="#1E2328" />
      <text x="74" y="49" textAnchor="middle" fontFamily="Archivo, sans-serif" fontWeight="800" fontSize="22" fill="#1E2328">
        ProCargo
      </text>
      <path d="M148 26h36c6 0 10 3 13 8l13 20c2 3 3 6 3 9v9h-65z" fill="#0F5F5B" />
      <path d="M156 32h26c3 0 5 1 7 4l9 14h-42z" fill="#0A4441" />
      <rect x="146" y="72" width="70" height="6" rx="2" fill="#1E2328" />
      <rect x="2" y="72" width="146" height="6" rx="2" fill="#1E2328" />
      <rect x="196" y="64" width="18" height="8" rx="1.5" fill="#F5C518" stroke="#1E2328" strokeWidth="1" />
      {wheel(34)}
      {wheel(66)}
      {wheel(182)}
    </svg>
  );
}

/** The same lorry seen from above, heading down the road. */
function LorryTop() {
  return (
    <svg viewBox="0 0 60 150" aria-hidden="true" style={{ display: 'block', width: '100%' }}>
      <rect x="6" y="2" width="48" height="104" rx="4" fill="#FFFFFF" />
      <rect x="6" y="2" width="48" height="8" rx="3" fill="#0F5F5B" />
      <rect x="10" y="112" width="40" height="34" rx="7" fill="#0F5F5B" />
      <rect x="14" y="128" width="32" height="12" rx="3" fill="#0A4441" />
      <circle cx="16" cy="146" r="3" fill="#F2F4F3" />
      <circle cx="44" cy="146" r="3" fill="#F2F4F3" />
    </svg>
  );
}
