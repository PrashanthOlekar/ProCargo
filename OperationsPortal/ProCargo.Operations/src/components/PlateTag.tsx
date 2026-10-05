import { Box, Chip, type SxProps, type Theme } from '@mui/material';
import { palette } from '../theme';
import type { StatusInfo, Tone } from '../lib/statuses';

/**
 * Booking, trip and vehicle numbers shown as the yellow commercial number plate every Indian goods vehicle
 * carries — the one bold element in the interface, so a reference number is always easy to find and read out
 * over the phone.
 */
export function PlateTag({ children, size = 'medium', sx }: { children: string; size?: 'small' | 'medium' | 'large'; sx?: SxProps<Theme> }) {
  const font = size === 'large' ? '1.5rem' : size === 'small' ? '0.78rem' : '0.92rem';
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-block',
        bgcolor: palette.plate,
        color: palette.asphalt,
        border: `2px solid ${palette.asphalt}`,
        borderRadius: '4px',
        px: size === 'large' ? 1.5 : 0.9,
        py: size === 'large' ? 0.4 : 0.1,
        fontWeight: 800,
        fontStretch: '80%',
        fontSize: font,
        letterSpacing: '0.04em',
        whiteSpace: 'nowrap',
        lineHeight: 1.5,
        ...sx,
      }}
    >
      {children}
    </Box>
  );
}

const toneStyles: Record<Tone, { bg: string; fg: string }> = {
  neutral: { bg: '#ECEFED', fg: palette.slate },
  progress: { bg: palette.tealTint, fg: palette.tealDark },
  good: { bg: '#E3F1E4', fg: '#1F5E23' },
  bad: { bg: '#F8E3E1', fg: '#8C1D17' },
  warn: { bg: '#FDF1D2', fg: '#7A5200' },
};

export function StatusChip({ status, size = 'small' }: { status: StatusInfo | undefined; size?: 'small' | 'medium' }) {
  if (!status) return null;
  const style = toneStyles[status.tone];
  return <Chip size={size} label={status.label} sx={{ bgcolor: style.bg, color: style.fg, borderRadius: '4px' }} />;
}
