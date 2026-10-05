import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ThemeProvider } from '@mui/material';
import { theme } from '../theme';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { JourneyBar } from '../components/Timeline';
import { bookingJourney, tripStatus } from '../lib/statuses';

const wrap = (ui: React.ReactElement) => render(<ThemeProvider theme={theme}>{ui}</ThemeProvider>);

describe('components', () => {
  it('renders reference numbers as plates', () => {
    wrap(<PlateTag>PC-BKG-2026-000001</PlateTag>);
    expect(screen.getByText('PC-BKG-2026-000001')).toBeInTheDocument();
  });

  it('renders status labels', () => {
    wrap(<StatusChip status={tripStatus[3]} />);
    expect(screen.getByText('In transit')).toBeInTheDocument();
  });

  it('marks reached journey stages for assistive technology', () => {
    wrap(<JourneyBar statusId={5} stages={bookingJourney} />);
    const current = screen.getAllByRole('listitem').filter((li) => li.getAttribute('aria-current') === 'step');
    expect(current).toHaveLength(3);
  });
});
