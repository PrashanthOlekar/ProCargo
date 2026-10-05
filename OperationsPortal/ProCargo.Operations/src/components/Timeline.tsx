import { Box, Typography } from '@mui/material';
import type { StatusHistory } from '../api/types';
import type { StatusInfo } from '../lib/statuses';
import { formatDateTime } from '../lib/format';
import { palette } from '../theme';

/** Vertical history of status changes, newest last. */
export function StatusTimeline({ history, statuses }: { history: StatusHistory[]; statuses: Record<number, StatusInfo> }) {
  return (
    <Box component="ol" sx={{ listStyle: 'none', m: 0, p: 0 }}>
      {history.map((h, index) => (
        <Box component="li" key={h.historyId} sx={{ display: 'grid', gridTemplateColumns: '20px 1fr', columnGap: 1.5 }}>
          <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
            <Box sx={{ width: 12, height: 12, mt: 0.75, borderRadius: '50%', bgcolor: index === history.length - 1 ? palette.teal : palette.line }} />
            {index < history.length - 1 && <Box sx={{ flex: 1, width: 2, bgcolor: palette.line }} />}
          </Box>
          <Box sx={{ pb: 2 }}>
            <Typography sx={{ fontWeight: 650 }}>{statuses[h.toStatusId]?.label ?? `Status ${h.toStatusId}`}</Typography>
            <Typography variant="body2" color="text.secondary">
              {formatDateTime(h.changedDateUtc)}
              {h.changedByName ? ` by ${h.changedByName}` : ''}
            </Typography>
            {h.remarks && <Typography variant="body2" sx={{ mt: 0.25 }}>{h.remarks}</Typography>}
          </Box>
        </Box>
      ))}
    </Box>
  );
}

/** Customer-facing journey: one bar per stage, filled up to where the booking has reached. */
export function JourneyBar({ statusId, stages }: { statusId: number; stages: { label: string; reachedAt: number[] }[] }) {
  return (
    <Box
      component="ol"
      aria-label="Booking progress"
      sx={{ listStyle: 'none', m: 0, p: 0, display: 'grid', gridTemplateColumns: `repeat(${stages.length}, 1fr)`, gap: 0.5 }}
    >
      {stages.map((stage) => {
        const reached = stage.reachedAt.includes(statusId);
        return (
          <Box component="li" key={stage.label} aria-current={reached ? 'step' : undefined}>
            <Box sx={{ height: 6, borderRadius: 3, bgcolor: reached ? palette.teal : palette.line }} />
            <Typography
              variant="body2"
              sx={{ mt: 0.75, fontSize: '0.78rem', color: reached ? 'text.primary' : 'text.secondary', fontWeight: reached ? 650 : 400 }}
            >
              {stage.label}
            </Typography>
          </Box>
        );
      })}
    </Box>
  );
}
