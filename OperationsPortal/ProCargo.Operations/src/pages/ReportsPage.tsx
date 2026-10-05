import { useState, type ReactNode } from 'react';
import {
  Box,
  Button,
  Grid,
  MenuItem,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from '@mui/material';
import FileDownloadOutlined from '@mui/icons-material/FileDownloadOutlined';
import dayjs from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { reportApi } from '../api/endpoints';
import { downloadFile, toApiError } from '../api/client';
import { P, useAuth } from '../auth/AuthContext';
import { useNotify } from '../components/Forms';
import { EmptyState, PageHeader, QueryView, Section, StatTile } from '../components/Layout';
import { formatMoney, formatNumber } from '../lib/format';

type Period = { from: string; to: string };

const presets: { label: string; period: () => Period }[] = [
  { label: 'Last 7 days', period: () => ({ from: dayjs().subtract(6, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }) },
  { label: 'Last 30 days', period: () => ({ from: dayjs().subtract(29, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }) },
  { label: 'This month', period: () => ({ from: dayjs().startOf('month').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }) },
  {
    label: 'Last month',
    period: () => ({ from: dayjs().subtract(1, 'month').startOf('month').format('YYYY-MM-DD'), to: dayjs().subtract(1, 'month').endOf('month').format('YYYY-MM-DD') }),
  },
  { label: 'This financial year', period: () => {
    const now = dayjs();
    const start = now.month() >= 3 ? now.startOf('year').month(3) : now.subtract(1, 'year').startOf('year').month(3);
    return { from: start.format('YYYY-MM-DD'), to: now.format('YYYY-MM-DD') };
  } },
];

/** Horizontal bar used in every report: the value's share of the largest value in the table. */
function Bar({ value, max, tone = 'primary.main' }: { value: number; max: number; tone?: string }) {
  const pct = max > 0 ? Math.max(2, Math.round((value / max) * 100)) : 0;
  return (
    <Box sx={{ height: 8, borderRadius: 4, bgcolor: 'action.hover', minWidth: 80 }} aria-hidden>
      <Box sx={{ height: 8, borderRadius: 4, width: `${pct}%`, bgcolor: tone }} />
    </Box>
  );
}

function ReportTable({ head, rows, empty }: { head: { label: string; align?: 'right' }[]; rows: ReactNode[][]; empty: string }) {
  if (rows.length === 0) return <EmptyState title={empty} />;
  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>{head.map((h, i) => <TableCell key={i} align={h.align}>{h.label}</TableCell>)}</TableRow>
        </TableHead>
        <TableBody>
          {rows.map((r, i) => (
            <TableRow key={i}>{r.map((c, j) => <TableCell key={j} align={head[j]?.align}>{c}</TableCell>)}</TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

export function ReportsPage() {
  const { can } = useAuth();
  const finance = can(P.ViewFinance);
  const tabs = ['Bookings', 'Trip performance', ...(finance ? ['Revenue', 'Settlements'] : []), 'Top customers', 'Partners'];
  const [tab, setTab] = useState(0);
  const [period, setPeriod] = useState<Period>(presets[1].period());
  const [groupBy, setGroupBy] = useState<'Day' | 'Month'>('Day');
  const notify = useNotify();
  const current = tabs[tab];
  const invalid = !period.from || !period.to || period.from > period.to;

  const exportName: Record<string, string> = {
    Bookings: 'bookings-by-day',
    'Trip performance': 'trip-performance',
    Revenue: 'revenue',
    Settlements: 'settlements',
    'Top customers': 'top-customers',
  };

  const exportCsv = async () => {
    const report = exportName[current];
    try {
      await downloadFile(reportApi.exportUrl(report, period.from, period.to, groupBy), `procargo-${report}.csv`);
    } catch (e) {
      notify(toApiError(e).message, 'error');
    }
  };

  return (
    <>
      <PageHeader
        title="Reports"
        subtitle="Dates are Indian Standard Time calendar days, both ends included."
        actions={
          exportName[current] && (
            <Button variant="outlined" startIcon={<FileDownloadOutlined />} onClick={exportCsv} disabled={invalid}>
              Download CSV
            </Button>
          )
        }
      />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2, alignItems: { sm: 'center' }, flexWrap: 'wrap' }} useFlexGap>
        <TextField type="date" label="From" value={period.from} onChange={(e) => setPeriod({ ...period, from: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
        <TextField
          type="date"
          label="To"
          value={period.to}
          onChange={(e) => setPeriod({ ...period, to: e.target.value })}
          error={invalid}
          helperText={invalid ? 'The end date must be on or after the start' : undefined}
          slotProps={{ inputLabel: { shrink: true } }}
        />
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }} useFlexGap>
          {presets.map((p) => (
            <Button key={p.label} size="small" onClick={() => setPeriod(p.period())}>{p.label}</Button>
          ))}
        </Stack>
      </Stack>
      <Tabs value={tab} onChange={(_, t) => setTab(t)} variant="scrollable" sx={{ mb: 2 }}>
        {tabs.map((t) => <Tab key={t} label={t} />)}
      </Tabs>
      {invalid ? null : (
        <>
          {current === 'Bookings' && <BookingsReport period={period} />}
          {current === 'Trip performance' && <TripReport period={period} />}
          {current === 'Revenue' && <RevenueReport period={period} groupBy={groupBy} setGroupBy={setGroupBy} />}
          {current === 'Settlements' && <SettlementReport period={period} />}
          {current === 'Top customers' && <CustomersReport period={period} />}
          {current === 'Partners' && <PartnerReport />}
        </>
      )}
    </>
  );
}

function BookingsReport({ period }: { period: Period }) {
  const byDay = useQuery({ queryKey: ['report', 'byDay', period], queryFn: () => reportApi.bookingsByDay(period.from, period.to) });
  const byStatus = useQuery({ queryKey: ['report', 'byStatus', period], queryFn: () => reportApi.bookingsByStatus(period.from, period.to) });
  return (
    <Grid container spacing={3}>
      <Grid size={{ xs: 12, lg: 7 }}>
        <Section title="Bookings per day">
          <QueryView query={byDay}>
            {(rows) => {
              const max = Math.max(0, ...rows.map((r) => r.totalBookings));
              const total = rows.reduce((s, r) => s + r.totalBookings, 0);
              const confirmed = rows.reduce((s, r) => s + r.confirmedBookings, 0);
              return (
                <>
                  <Grid container spacing={2} sx={{ mb: 2 }}>
                    <Grid size={{ xs: 6 }}><StatTile label="Bookings" value={formatNumber(total)} /></Grid>
                    <Grid size={{ xs: 6 }}><StatTile label="Confirmed" value={formatNumber(confirmed)} hint={total ? `${Math.round((confirmed / total) * 100)}% of bookings` : undefined} /></Grid>
                  </Grid>
                  <ReportTable
                    empty="No bookings in this period"
                    head={[{ label: 'Date' }, { label: 'Bookings', align: 'right' }, { label: '' }, { label: 'Confirmed', align: 'right' }, { label: 'Cancelled or rejected', align: 'right' }]}
                    rows={rows.map((r) => [dayjs(r.reportDate).format('ddd D MMM'), r.totalBookings, <Bar value={r.totalBookings} max={max} />, r.confirmedBookings, r.cancelledOrRejected])}
                  />
                </>
              );
            }}
          </QueryView>
        </Section>
      </Grid>
      <Grid size={{ xs: 12, lg: 5 }}>
        <Section title="Where bookings stand now">
          <QueryView query={byStatus}>
            {(rows) => {
              const max = Math.max(0, ...rows.map((r) => r.itemCount));
              return (
                <ReportTable
                  empty="No bookings in this period"
                  head={[{ label: 'Status' }, { label: 'Count', align: 'right' }, { label: '' }]}
                  rows={rows.map((r) => [r.statusName, r.itemCount, <Bar value={r.itemCount} max={max} tone="secondary.main" />])}
                />
              );
            }}
          </QueryView>
        </Section>
      </Grid>
    </Grid>
  );
}

function TripReport({ period }: { period: Period }) {
  const q = useQuery({ queryKey: ['report', 'trips', period], queryFn: () => reportApi.tripPerformance(period.from, period.to) });
  return (
    <Section title="Trip performance by vehicle type" >
      <Typography color="text.secondary" sx={{ mb: 2 }}>On time means delivered no later than the expected delivery time on the booking.</Typography>
      <QueryView query={q}>
        {(rows) => (
          <ReportTable
            empty="No trips in this period"
            head={[
              { label: 'Vehicle type' },
              { label: 'Trips', align: 'right' },
              { label: 'Delivered', align: 'right' },
              { label: 'On time', align: 'right' },
              { label: 'On-time rate' },
              { label: 'Cancelled', align: 'right' },
              { label: 'Avg transit', align: 'right' },
            ]}
            rows={rows.map((r) => {
              const rate = r.deliveredCount ? r.onTimeCount / r.deliveredCount : 0;
              return [
                r.vehicleTypeName,
                r.tripCount,
                r.deliveredCount,
                r.onTimeCount,
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <Box sx={{ flex: 1 }}><Bar value={rate} max={1} tone={rate >= 0.9 ? 'success.main' : rate >= 0.75 ? 'warning.main' : 'error.main'} /></Box>
                  <Typography variant="body2" sx={{ width: 40 }}>{r.deliveredCount ? `${Math.round(rate * 100)}%` : '—'}</Typography>
                </Stack>,
                r.cancelledCount,
                r.averageTransitHours == null ? '—' : `${r.averageTransitHours.toFixed(1)} h`,
              ];
            })}
          />
        )}
      </QueryView>
    </Section>
  );
}

function RevenueReport({ period, groupBy, setGroupBy }: { period: Period; groupBy: 'Day' | 'Month'; setGroupBy: (g: 'Day' | 'Month') => void }) {
  const q = useQuery({ queryKey: ['report', 'revenue', period, groupBy], queryFn: () => reportApi.revenue(period.from, period.to, groupBy) });
  return (
    <Section
      title="Invoiced revenue"
      action={
        <TextField select size="small" label="Group by" value={groupBy} onChange={(e) => setGroupBy(e.target.value as 'Day' | 'Month')} sx={{ width: 140 }}>
          <MenuItem value="Day">Day</MenuItem>
          <MenuItem value="Month">Month</MenuItem>
        </TextField>
      }
    >
      <QueryView query={q}>
        {(rows) => {
          const sum = (k: 'subTotal' | 'taxAmount' | 'totalAmount' | 'paidAmount') => rows.reduce((s, r) => s + r[k], 0);
          const max = Math.max(0, ...rows.map((r) => r.totalAmount));
          return (
            <>
              <Grid container spacing={2} sx={{ mb: 2 }}>
                <Grid size={{ xs: 6, md: 3 }}><StatTile label="Freight (before tax)" value={formatMoney(sum('subTotal'))} /></Grid>
                <Grid size={{ xs: 6, md: 3 }}><StatTile label="GST" value={formatMoney(sum('taxAmount'))} /></Grid>
                <Grid size={{ xs: 6, md: 3 }}><StatTile label="Invoiced" value={formatMoney(sum('totalAmount'))} /></Grid>
                <Grid size={{ xs: 6, md: 3 }}><StatTile label="Collected" value={formatMoney(sum('paidAmount'))} /></Grid>
              </Grid>
              <ReportTable
                empty="No invoices in this period"
                head={[{ label: 'Period' }, { label: 'Invoices', align: 'right' }, { label: 'Freight', align: 'right' }, { label: 'GST', align: 'right' }, { label: 'Total', align: 'right' }, { label: '' }, { label: 'Collected', align: 'right' }]}
                rows={rows.map((r) => [r.period, r.invoiceCount, formatMoney(r.subTotal), formatMoney(r.taxAmount), formatMoney(r.totalAmount), <Bar value={r.totalAmount} max={max} />, formatMoney(r.paidAmount)])}
              />
            </>
          );
        }}
      </QueryView>
    </Section>
  );
}

function SettlementReport({ period }: { period: Period }) {
  const q = useQuery({ queryKey: ['report', 'settlements', period], queryFn: () => reportApi.settlements(period.from, period.to) });
  return (
    <Section title="Owner settlements">
      <QueryView query={q}>
        {(rows) => (
          <ReportTable
            empty="No settlements in this period"
            head={[{ label: 'Status' }, { label: 'Settlements', align: 'right' }, { label: 'Gross freight', align: 'right' }, { label: 'Commission', align: 'right' }, { label: 'Net to owners', align: 'right' }]}
            rows={rows.map((r) => [r.statusName, r.settlementCount, formatMoney(r.grossAmount), formatMoney(r.commissionAmount), formatMoney(r.netAmount)])}
          />
        )}
      </QueryView>
    </Section>
  );
}

function CustomersReport({ period }: { period: Period }) {
  const q = useQuery({ queryKey: ['report', 'customers', period], queryFn: () => reportApi.topCustomers(period.from, period.to) });
  return (
    <Section title="Top ten customers by invoiced amount">
      <QueryView query={q}>
        {(rows) => {
          const max = Math.max(0, ...rows.map((r) => r.invoicedAmount));
          return (
            <ReportTable
              empty="No invoiced customers in this period"
              head={[{ label: 'Customer' }, { label: 'Bookings', align: 'right' }, { label: 'Invoiced', align: 'right' }, { label: '' }]}
              rows={rows.map((r) => [
                <>
                  <Typography sx={{ fontWeight: 600 }}>{r.customerName}</Typography>
                  <Typography variant="body2" color="text.secondary">{r.customerNumber}</Typography>
                </>,
                r.bookingCount,
                formatMoney(r.invoicedAmount),
                <Bar value={r.invoicedAmount} max={max} />,
              ])}
            />
          );
        }}
      </QueryView>
    </Section>
  );
}

function PartnerReport() {
  const q = useQuery({ queryKey: ['report', 'partners'], queryFn: reportApi.partnerSummary });
  return (
    <QueryView query={q}>
      {(rows) => {
        const groups = Array.from(new Set(rows.map((r) => r.entityType)));
        return (
          <Grid container spacing={3}>
            {groups.map((g) => (
              <Grid key={g} size={{ xs: 12, sm: 6, lg: 4 }}>
                <Section title={g}>
                  <ReportTable
                    empty="None"
                    head={[{ label: 'Verification' }, { label: 'Count', align: 'right' }]}
                    rows={rows.filter((r) => r.entityType === g).map((r) => [r.statusName, r.itemCount])}
                  />
                </Section>
              </Grid>
            ))}
            {groups.length === 0 && <Grid size={12}><EmptyState title="No partners registered yet" /></Grid>}
          </Grid>
        );
      }}
    </QueryView>
  );
}
