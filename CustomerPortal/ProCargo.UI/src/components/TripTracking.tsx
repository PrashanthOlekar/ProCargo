import { Box, Link, Stack, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { tripApi } from '../api/endpoints';
import { fromNow, formatDateTime } from '../lib/format';
import { palette } from '../theme';

interface Point {
  latitude: number;
  longitude: number;
}

/**
 * Lightweight live-tracking view without a map SDK: the route is drawn to scale between pickup and drop, with the
 * driver's breadcrumb trail. "Open in Maps" hands the last position to the phone's map app for street detail.
 * Polls every 30 seconds while the trip is moving.
 */
export function TripTrackingPanel({ tripId, pickup, delivery, active }: { tripId: number; pickup?: Point | null; delivery?: Point | null; active: boolean }) {
  const tracking = useQuery({
    queryKey: ['trip', tripId, 'tracking'],
    queryFn: () => tripApi.tracking(tripId),
    refetchInterval: active ? 30_000 : false,
  });

  const points = tracking.data?.points ?? [];
  const last = tracking.data?.lastLocation;
  const all: Point[] = [...(pickup ? [pickup] : []), ...(delivery ? [delivery] : []), ...points];

  if (tracking.isLoading) return <Typography color="text.secondary">Loading the truck's position…</Typography>;
  if (all.length === 0) return <Typography color="text.secondary">Tracking starts when the driver picks up your goods.</Typography>;

  const lats = all.map((p) => p.latitude);
  const lngs = all.map((p) => p.longitude);
  const minLat = Math.min(...lats);
  const maxLat = Math.max(...lats);
  const minLng = Math.min(...lngs);
  const maxLng = Math.max(...lngs);
  const span = Math.max(maxLat - minLat, maxLng - minLng, 0.01);
  const W = 600;
  const H = 260;
  const pad = 28;
  const x = (lng: number) => pad + ((lng - minLng) / span) * (W - 2 * pad);
  const y = (lat: number) => H - pad - ((lat - minLat) / span) * (H - 2 * pad);

  return (
    <Box>
      <Box
        component="svg"
        viewBox={`0 0 ${W} ${H}`}
        role="img"
        aria-label={last ? `Truck last seen ${fromNow(last.recordedDateUtc)}` : 'Route from pickup to delivery'}
        sx={{ width: '100%', height: 'auto', bgcolor: '#F1F4F2', borderRadius: 1.5, border: 1, borderColor: 'divider' }}
      >
        {pickup && delivery && (
          <line x1={x(pickup.longitude)} y1={y(pickup.latitude)} x2={x(delivery.longitude)} y2={y(delivery.latitude)} stroke={palette.line} strokeWidth={6} strokeLinecap="round" strokeDasharray="2 12" />
        )}
        {points.length > 1 && (
          <polyline points={points.map((p) => `${x(p.longitude)},${y(p.latitude)}`).join(' ')} fill="none" stroke={palette.teal} strokeWidth={4} strokeLinejoin="round" strokeLinecap="round" />
        )}
        {pickup && (
          <g>
            <circle cx={x(pickup.longitude)} cy={y(pickup.latitude)} r={8} fill="#fff" stroke={palette.asphalt} strokeWidth={3} />
            <text x={x(pickup.longitude) + 12} y={y(pickup.latitude) + 4} fontSize={14} fontWeight={700} fill={palette.asphalt}>Pickup</text>
          </g>
        )}
        {delivery && (
          <g>
            <rect x={x(delivery.longitude) - 8} y={y(delivery.latitude) - 8} width={16} height={16} fill={palette.asphalt} />
            <text x={x(delivery.longitude) + 12} y={y(delivery.latitude) + 4} fontSize={14} fontWeight={700} fill={palette.asphalt}>Drop</text>
          </g>
        )}
        {last && (
          <g>
            <circle cx={x(last.longitude)} cy={y(last.latitude)} r={11} fill={palette.plate} stroke={palette.asphalt} strokeWidth={3} />
          </g>
        )}
      </Box>
      {last ? (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 1.25, justifyContent: 'space-between' }}>
          <Typography variant="body2" color="text.secondary">
            Last position {fromNow(last.recordedDateUtc)} ({formatDateTime(last.recordedDateUtc)})
            {last.speedKmph != null ? `, moving at ${Math.round(last.speedKmph)} km/h` : ''}
          </Typography>
          <Link href={`https://www.google.com/maps?q=${last.latitude},${last.longitude}`} target="_blank" rel="noopener noreferrer" variant="body2">
            Open in Maps
          </Link>
        </Stack>
      ) : (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1.25 }}>
          No position received yet.
        </Typography>
      )}
    </Box>
  );
}
