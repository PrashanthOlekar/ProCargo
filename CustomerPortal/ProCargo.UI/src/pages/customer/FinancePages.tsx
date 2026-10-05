import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Grid,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { dashboardApi, financeApi, bookingApi } from '../../api/endpoints';
import type { InvoiceListItem, PaymentInitiated } from '../../api/types';
import { newIdempotencyKey, toApiError } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { DataTable } from '../../components/DataTable';
import { useNotify } from '../../components/Forms';
import { DetailList, EmptyState, PageHeader, QueryView, Section, StatTile } from '../../components/Layout';
import { PlateTag, StatusChip } from '../../components/PlateTag';
import { formatDate, formatDateTime, formatMoney } from '../../lib/format';
import { bookingStatus, invoiceStatus, paymentMethod, paymentStatus } from '../../lib/statuses';

export function CustomerDashboardPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const summary = useQuery({ queryKey: ['dashboard', 'customer'], queryFn: dashboardApi.customer });
  const recent = useQuery({ queryKey: ['bookings', 'recent'], queryFn: () => bookingApi.list({ pageSize: 5 }) });

  return (
    <>
      <PageHeader
        title={`Hello, ${user?.fullName.split(' ')[0]}`}
        subtitle="Your loads at a glance."
        actions={
          <Button component={RouterLink} to="/customer/bookings/new" variant="contained" size="large">
            Book a lorry
          </Button>
        }
      />
      <QueryView query={summary}>
        {(s) => (
          <>
            {s.awaitingQuotationResponse > 0 && (
              <Alert severity="warning" sx={{ mb: 2.5 }} action={<Button color="inherit" component={RouterLink} to="/customer/bookings">Review</Button>}>
                {s.awaitingQuotationResponse === 1 ? 'A quotation is' : `${s.awaitingQuotationResponse} quotations are`} waiting for your decision.
              </Alert>
            )}
            <Grid container spacing={2} sx={{ mb: 3 }}>
              <Grid size={{ xs: 6, md: 3 }}>
                <StatTile label="Active bookings" value={s.activeBookings} />
              </Grid>
              <Grid size={{ xs: 6, md: 3 }}>
                <StatTile label="On the road now" value={s.inTransit} />
              </Grid>
              <Grid size={{ xs: 6, md: 3 }}>
                <StatTile label="To pay" value={formatMoney(s.outstandingAmount)} />
              </Grid>
              <Grid size={{ xs: 6, md: 3 }}>
                <StatTile label="Delivered" value={s.completedBookings} />
              </Grid>
            </Grid>
          </>
        )}
      </QueryView>
      <Section title="Recent bookings" action={<Button component={RouterLink} to="/customer/bookings">See all</Button>}>
        <QueryView query={recent}>
          {(page) =>
            page.items.length === 0 ? (
              <EmptyState title="Nothing booked yet" action={<Button component={RouterLink} to="/customer/bookings/new" variant="contained">Book your first lorry</Button>}>
                Most quotations arrive within the hour during working hours.
              </EmptyState>
            ) : (
              <Stack divider={<Box sx={{ borderTop: 1, borderColor: 'divider' }} />}>
                {page.items.map((b) => (
                  <Stack
                    key={b.bookingId}
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{ py: 1.5, cursor: 'pointer', justifyContent: 'space-between' }}
                    onClick={() => navigate(`/customer/bookings/${b.bookingId}`)}
                  >
                    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                      <PlateTag size="small">{b.bookingNumber}</PlateTag>
                      <Typography sx={{ fontWeight: 600 }}>
                        {b.pickupCityName} to {b.deliveryCityName}
                      </Typography>
                    </Stack>
                    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                      <Typography variant="body2" color="text.secondary">
                        {formatDate(b.requestedPickupDateUtc)}
                      </Typography>
                      <StatusChip status={bookingStatus[b.bookingStatusId]} />
                    </Stack>
                  </Stack>
                ))}
              </Stack>
            )
          }
        </QueryView>
      </Section>
    </>
  );
}

export function InvoicesPage() {
  const navigate = useNavigate();
  return (
    <>
      <PageHeader title="Invoices & payments" subtitle="Invoices are issued after delivery for the price you accepted." />
      <DataTable<InvoiceListItem>
        queryKey={['invoices']}
        fetcher={(q) => financeApi.invoices(q)}
        rowKey={(i) => i.invoiceId}
        onRowClick={(i) => navigate(`/customer/invoices/${i.invoiceId}`)}
        searchPlaceholder="Search by invoice or booking number"
        empty={{ title: 'No invoices yet', text: 'Your first invoice appears here once a delivery is completed.' }}
        columns={[
          { header: 'Invoice', primary: true, render: (i) => <Typography sx={{ fontWeight: 650 }}>{i.invoiceNumber}</Typography> },
          { header: 'Booking', render: (i) => <PlateTag size="small">{i.bookingNumber}</PlateTag> },
          { header: 'Issued', render: (i) => formatDate(i.invoiceDateUtc) },
          { header: 'Due', primary: true, render: (i) => <Typography color={i.isOverdue ? 'error' : undefined}>{formatDate(i.dueDateUtc)}{i.isOverdue ? ' (overdue)' : ''}</Typography> },
          { header: 'Amount', primary: true, align: 'right', render: (i) => formatMoney(i.totalAmount) },
          { header: 'Status', primary: true, render: (i) => <StatusChip status={invoiceStatus[i.invoiceStatusId]} /> },
        ]}
      />
    </>
  );
}

declare global {
  interface Window {
    Razorpay?: new (options: Record<string, unknown>) => { open: () => void; on: (event: string, cb: (r: unknown) => void) => void };
  }
}

function loadRazorpay(): Promise<void> {
  if (window.Razorpay) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://checkout.razorpay.com/v1/checkout.js';
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('The payment window could not be loaded. Check your connection and try again.'));
    document.body.appendChild(script);
  });
}

export function InvoiceDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['invoice', id], queryFn: () => financeApi.invoice(id) });
  const { user } = useAuth();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [method, setMethod] = useState('Upi');
  const [paying, setPaying] = useState(false);
  const [sandbox, setSandbox] = useState<PaymentInitiated | null>(null);
  const [error, setError] = useState('');

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ['invoice', id] });
    queryClient.invalidateQueries({ queryKey: ['invoices'] });
  };

  const pay = async () => {
    setError('');
    setPaying(true);
    try {
      // One idempotency key per click: a double click or network retry cannot create two payments.
      const payment = await financeApi.initiatePayment(id, method, newIdempotencyKey());
      if (payment.checkout.gateway === 'Sandbox') {
        setSandbox(payment);
        return;
      }
      await loadRazorpay();
      const checkout = new window.Razorpay!({
        key: payment.checkout.publicKey,
        order_id: payment.checkout.orderId,
        amount: Math.round(payment.checkout.amount * 100),
        currency: payment.checkout.currency,
        name: 'ProCargo Logistics',
        description: `Payment ${payment.paymentNumber}`,
        prefill: { email: user?.email, contact: user?.phoneNumber },
        theme: { color: '#0F5F5B' },
        handler: async (r: { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string }) => {
          try {
            await financeApi.confirmPayment(payment.paymentId, {
              gatewayOrderId: r.razorpay_order_id,
              gatewayPaymentId: r.razorpay_payment_id,
              signature: r.razorpay_signature,
            });
            notify('Payment received. Thank you!');
          } catch (e) {
            notify(`${toApiError(e).message} If money left your account it will be matched automatically.`, 'error');
          } finally {
            refresh();
          }
        },
        modal: { ondismiss: () => setPaying(false) },
      });
      checkout.open();
    } catch (e) {
      setError(toApiError(e).message);
    } finally {
      setPaying(false);
    }
  };

  return (
    <QueryView query={query}>
      {({ invoice: inv, items, payments, availableActions }) => (
        <>
          <PageHeader
            back={
              <Button component={RouterLink} to="/customer/invoices" startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
                Invoices
              </Button>
            }
            title={`Invoice ${inv.invoiceNumber}`}
            subtitle={<StatusChip status={invoiceStatus[inv.invoiceStatusId]} size="medium" />}
            actions={
              <Button onClick={() => window.print()}>Print</Button>
            }
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Details">
                <DetailList
                  items={[
                    ['Billed to', inv.customerCompanyName ?? inv.customerName],
                    ['GSTIN', inv.customerGstNumber],
                    ['Booking', <PlateTag size="small">{inv.bookingNumber}</PlateTag>],
                    ['Trip', inv.tripNumber],
                    ['Issued', formatDate(inv.invoiceDateUtc)],
                    ['Due', formatDate(inv.dueDateUtc)],
                  ]}
                />
                <Table size="small" sx={{ mt: 3 }}>
                  <TableHead>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>Charge</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>
                        Amount
                      </TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {items.map((i) => (
                      <TableRow key={i.invoiceItemId}>
                        <TableCell sx={{ pl: 0 }}>{i.description}</TableCell>
                        <TableCell align="right" sx={{ pr: 0 }}>
                          {formatMoney(i.amount)}
                        </TableCell>
                      </TableRow>
                    ))}
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>Taxable value</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>
                        {formatMoney(inv.subTotal)}
                      </TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0 }}>GST {inv.taxPercent}%</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>
                        {formatMoney(inv.taxAmount)}
                      </TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell sx={{ pl: 0, fontWeight: 800 }}>Total</TableCell>
                      <TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>
                        {formatMoney(inv.totalAmount)}
                      </TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title={inv.balanceAmount > 0 ? 'Amount due' : 'Paid in full'}>
                <Typography sx={{ fontSize: '2.2rem', fontWeight: 800, fontStretch: '80%' }}>
                  {formatMoney(inv.balanceAmount > 0 ? inv.balanceAmount : inv.totalAmount)}
                </Typography>
                {availableActions.includes('Pay') && (
                  <>
                    <TextField select label="Pay with" value={method} onChange={(e) => setMethod(e.target.value)} sx={{ mt: 2 }}>
                      <MenuItem value="Upi">UPI</MenuItem>
                      <MenuItem value="Card">Debit or credit card</MenuItem>
                      <MenuItem value="NetBanking">Net banking</MenuItem>
                      <MenuItem value="Wallet">Wallet</MenuItem>
                    </TextField>
                    {error && (
                      <Alert severity="error" sx={{ mt: 2 }}>
                        {error}
                      </Alert>
                    )}
                    <Button variant="contained" size="large" fullWidth sx={{ mt: 2 }} onClick={pay} disabled={paying}>
                      {paying ? 'Opening payment…' : `Pay ${formatMoney(inv.balanceAmount)}`}
                    </Button>
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
                      Card and UPI details are entered on the bank's secure page; ProCargo never sees them. You can also pay by bank
                      transfer quoting {inv.invoiceNumber}.
                    </Typography>
                  </>
                )}
              </Section>
              {payments.length > 0 && (
                <Section title="Payments">
                  <Stack spacing={1.25}>
                    {payments.map((p) => (
                      <Stack key={p.paymentId} direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                        <Box>
                          <Typography sx={{ fontWeight: 600 }}>{formatMoney(p.amount)}</Typography>
                          <Typography variant="body2" color="text.secondary">
                            {paymentMethod[p.paymentMethodId]}, {formatDateTime(p.paymentDateUtc ?? p.createdDateUtc)}
                          </Typography>
                        </Box>
                        <StatusChip status={paymentStatus[p.paymentStatusId]} />
                      </Stack>
                    ))}
                  </Stack>
                </Section>
              )}
            </Grid>
          </Grid>

          <Dialog open={!!sandbox} onClose={() => setSandbox(null)} maxWidth="xs" fullWidth>
            <DialogTitle>Test payment</DialogTitle>
            <DialogContent>
              <DialogContentText>
                This environment uses the sandbox gateway, so no money moves. Complete the payment of{' '}
                {formatMoney(sandbox?.checkout.amount ?? 0)} to see the full flow.
              </DialogContentText>
            </DialogContent>
            <DialogActions sx={{ px: 3, pb: 2 }}>
              <Button onClick={() => setSandbox(null)}>Close</Button>
              <Button
                variant="contained"
                onClick={async () => {
                  try {
                    await financeApi.completeSandbox(sandbox!.paymentId);
                    notify('Payment received. Thank you!');
                  } catch (e) {
                    notify(toApiError(e).message, 'error');
                  } finally {
                    setSandbox(null);
                    refresh();
                  }
                }}
              >
                Complete test payment
              </Button>
            </DialogActions>
          </Dialog>
        </>
      )}
    </QueryView>
  );
}
