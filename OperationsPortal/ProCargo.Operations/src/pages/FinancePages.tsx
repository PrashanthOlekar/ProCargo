import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Grid, Stack, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { financeApi } from '../api/endpoints';
import type { EligibleTrip, InvoiceListItem, PaymentListItem, SettlementListItem } from '../api/types';
import { toApiError } from '../api/client';
import { P, useAuth } from '../auth/AuthContext';
import { DataTable } from '../components/DataTable';
import { FilterSelect, statusOptions, useDialog, useUrlFilter } from '../components/Filters';
import { ReasonDialog, useNotify } from '../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../components/Layout';
import { PlateTag, StatusChip } from '../components/PlateTag';
import { RecordDialog } from '../components/Records';
import { formatDate, formatDateTime, formatMoney } from '../lib/format';
import { invoiceStatus, paymentMethod, paymentStatus, settlementStatus } from '../lib/statuses';
import { back } from './BookingPages';

const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => today().slice(0, 8) + '01';

function useRun() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  return async (fn: () => Promise<unknown>, message: string, keys: unknown[][]) => {
    try {
      await fn();
      notify(message);
      keys.forEach((k) => queryClient.invalidateQueries({ queryKey: k }));
      return true;
    } catch (e) {
      notify(toApiError(e).message, 'error');
      return false;
    }
  };
}

// ---------------- invoices ----------------

export function InvoicesPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useUrlFilter('status');
  const [overdue, setOverdue] = useUrlFilter('overdue');
  return (
    <>
      <PageHeader title="Invoices" />
      <DataTable<InvoiceListItem>
        queryKey={['invoices', status, overdue]}
        fetcher={(q) => financeApi.invoices({ ...q, status: status ? Number(status) : undefined, overdueOnly: overdue === '1' })}
        rowKey={(i) => i.invoiceId}
        onRowClick={(i) => navigate(`/invoices/${i.invoiceId}`)}
        searchPlaceholder="Invoice, booking or customer"
        filters={
          <>
            <FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(invoiceStatus)} />
            <FilterSelect label="Due" value={overdue} onChange={setOverdue} options={[{ value: '1', label: 'Overdue only' }]} width={160} />
          </>
        }
        empty={{ title: 'No invoices match' }}
        columns={[
          { header: 'Invoice', primary: true, render: (i) => <Typography sx={{ fontWeight: 650 }}>{i.invoiceNumber}</Typography> },
          { header: 'Customer', primary: true, render: (i) => i.customerName },
          { header: 'Booking', render: (i) => <PlateTag size="small">{i.bookingNumber}</PlateTag> },
          { header: 'Issued', render: (i) => formatDate(i.invoiceDateUtc) },
          { header: 'Due', render: (i) => <Typography variant="body2" color={i.isOverdue ? 'error' : undefined}>{formatDate(i.dueDateUtc)}</Typography> },
          { header: 'Total', primary: true, align: 'right', render: (i) => formatMoney(i.totalAmount) },
          { header: 'Paid', align: 'right', render: (i) => formatMoney(i.paidAmount) },
          { header: 'Status', primary: true, render: (i) => <StatusChip status={invoiceStatus[i.invoiceStatusId]} /> },
        ]}
      />
    </>
  );
}

type InvoiceDialog = 'adjust' | 'cancel' | 'record';

export function InvoiceDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['invoice', id], queryFn: () => financeApi.invoice(id) });
  const dialog = useDialog<InvoiceDialog>();
  const run = useRun();
  const navigate = useNavigate();

  return (
    <QueryView query={query}>
      {({ invoice: inv, items, payments, availableActions: a }) => (
        <>
          <PageHeader
            back={back('/invoices', 'Invoices')}
            title={`Invoice ${inv.invoiceNumber}`}
            subtitle={<StatusChip status={invoiceStatus[inv.invoiceStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('RecordPayment') && <Button variant="contained" onClick={() => dialog.show('record')}>Record payment</Button>}
                {a.includes('Adjust') && <Button onClick={() => dialog.show('adjust')}>Add adjustment</Button>}
                {a.includes('Cancel') && <Button color="error" onClick={() => dialog.show('cancel')}>Cancel invoice</Button>}
                <Button onClick={() => window.print()}>Print</Button>
              </>
            }
          />
          {inv.cancellationReason && <Alert severity="info" sx={{ mb: 2 }}>Cancelled: {inv.cancellationReason}</Alert>}
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Lines">
                <Table size="small">
                  <TableBody>
                    {items.map((i) => (
                      <TableRow key={i.invoiceItemId}>
                        <TableCell sx={{ pl: 0 }}>{i.description}{i.isAdjustment ? ' (adjustment)' : ''}</TableCell>
                        <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(i.amount)}</TableCell>
                      </TableRow>
                    ))}
                    <TableRow><TableCell sx={{ pl: 0 }}>Taxable value</TableCell><TableCell align="right" sx={{ pr: 0 }}>{formatMoney(inv.subTotal)}</TableCell></TableRow>
                    <TableRow><TableCell sx={{ pl: 0 }}>GST {inv.taxPercent}%</TableCell><TableCell align="right" sx={{ pr: 0 }}>{formatMoney(inv.taxAmount)}</TableCell></TableRow>
                    <TableRow><TableCell sx={{ pl: 0, fontWeight: 800 }}>Total</TableCell><TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>{formatMoney(inv.totalAmount)}</TableCell></TableRow>
                  </TableBody>
                </Table>
              </Section>
              <Section title="Payments">
                {payments.length === 0 ? (
                  <Typography color="text.secondary">No payments yet.</Typography>
                ) : (
                  <Table size="small">
                    <TableBody>
                      {payments.map((p) => (
                        <TableRow key={p.paymentId} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/payments/${p.paymentId}`)}>
                          <TableCell sx={{ pl: 0 }}>{p.paymentNumber}</TableCell>
                          <TableCell>{paymentMethod[p.paymentMethodId]}</TableCell>
                          <TableCell>{formatDateTime(p.paymentDateUtc ?? p.createdDateUtc)}</TableCell>
                          <TableCell align="right">{formatMoney(p.amount)}</TableCell>
                          <TableCell sx={{ pr: 0 }}><StatusChip status={paymentStatus[p.paymentStatusId]} /></TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="Summary">
                <DetailList
                  columns={1}
                  items={[
                    ['Customer', inv.customerCompanyName ?? inv.customerName],
                    ['GSTIN', inv.customerGstNumber],
                    ['Booking', <Button component={RouterLink} to={`/bookings/${inv.bookingId}`} sx={{ p: 0, minWidth: 0 }}>{inv.bookingNumber}</Button>],
                    ['Trip', <Button component={RouterLink} to={`/trips/${inv.tripId}`} sx={{ p: 0, minWidth: 0 }}>{inv.tripNumber}</Button>],
                    ['Issued', formatDate(inv.invoiceDateUtc)],
                    ['Due', formatDate(inv.dueDateUtc)],
                    ['Paid', formatMoney(inv.paidAmount)],
                    ['Balance', <Typography sx={{ fontWeight: 800 }}>{formatMoney(inv.balanceAmount)}</Typography>],
                  ]}
                />
              </Section>
            </Grid>
          </Grid>

          {dialog.is('adjust') && (
            <RecordDialog
              title="Add an adjustment"
              fields={[
                { name: 'description', label: 'Description', required: true, helper: 'Shown on the invoice, e.g. Extra waiting at delivery' },
                { name: 'amount', label: 'Amount (₹, negative for a credit)', type: 'number', required: true },
              ]}
              initial={{ description: '', amount: '' }}
              onClose={dialog.close}
              onSubmit={async (v) => {
                if (await run(() => financeApi.adjustInvoice(id, { ...v, rowVersion: inv.rowVersion }), 'Adjustment added', [['invoice', id]])) dialog.close();
              }}
            />
          )}
          {dialog.is('record') && (
            <RecordDialog
              title="Record a payment received"
              submitLabel="Record payment"
              fields={[
                { name: 'amount', label: 'Amount (₹)', type: 'number', required: true, width: 6 },
                { name: 'method', label: 'Method', type: 'select', required: true, width: 6, options: [{ value: 'BankTransfer', label: 'Bank transfer / NEFT / RTGS' }, { value: 'Cheque', label: 'Cheque' }, { value: 'Cash', label: 'Cash' }] },
                { name: 'referenceNumber', label: 'UTR / cheque / receipt number', required: true },
                { name: 'remarks', label: 'Remarks', type: 'multiline' },
              ]}
              initial={{ amount: inv.balanceAmount, method: 'BankTransfer', referenceNumber: '', remarks: '' }}
              onClose={dialog.close}
              onSubmit={async (v) => {
                if (await run(() => financeApi.recordOffline({ ...v, invoiceId: id }), 'Payment recorded', [['invoice', id], ['payments']])) dialog.close();
              }}
            />
          )}
          <ReasonDialog open={dialog.is('cancel')} title="Cancel this invoice?" description="Only unpaid invoices can be cancelled. The booking returns to Delivered so a corrected invoice can be issued." confirmLabel="Cancel invoice" destructive onClose={dialog.close} onSubmit={(r) => run(() => financeApi.cancelInvoice(id, r), 'Invoice cancelled', [['invoice', id]])} />
        </>
      )}
    </QueryView>
  );
}

// ---------------- payments ----------------

export function PaymentsPage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState(0);
  const [status, setStatus] = useUrlFilter('status');
  const [from, setFrom] = useState(monthStart());
  const [to, setTo] = useState(today());
  const reconciliation = useQuery({
    queryKey: ['reconciliation', from, to],
    queryFn: () => financeApi.reconciliation(new Date(from).toISOString(), new Date(Date.parse(to) + 86_400_000).toISOString()),
    enabled: tab === 1,
  });

  return (
    <>
      <PageHeader title="Payments" />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} sx={{ mb: 2 }}>
        <Tab label="All payments" />
        <Tab label="Reconciliation" />
      </Tabs>
      {tab === 0 ? (
        <DataTable<PaymentListItem>
          queryKey={['payments', status]}
          fetcher={(q) => financeApi.payments({ ...q, status: status ? Number(status) : undefined })}
          rowKey={(p) => p.paymentId}
          onRowClick={(p) => navigate(`/payments/${p.paymentId}`)}
          searchPlaceholder="Payment or invoice number"
          filters={<FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(paymentStatus)} />}
          empty={{ title: 'No payments match' }}
          columns={[
            { header: 'Payment', primary: true, render: (p) => <Typography sx={{ fontWeight: 650 }}>{p.paymentNumber}</Typography> },
            { header: 'Customer', primary: true, render: (p) => p.customerName },
            { header: 'Invoice', render: (p) => p.invoiceNumber },
            { header: 'Method', render: (p) => `${paymentMethod[p.paymentMethodId]} (${p.gatewayName})` },
            { header: 'Date', render: (p) => formatDateTime(p.paymentDateUtc ?? p.createdDateUtc) },
            { header: 'Amount', primary: true, align: 'right', render: (p) => formatMoney(p.amount) },
            { header: 'Status', primary: true, render: (p) => <StatusChip status={paymentStatus[p.paymentStatusId]} /> },
          ]}
        />
      ) : (
        <Section>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }}>
            <TextField label="From" type="date" value={from} onChange={(e) => setFrom(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} sx={{ maxWidth: 200 }} />
            <TextField label="To" type="date" value={to} onChange={(e) => setTo(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} sx={{ maxWidth: 200 }} />
          </Stack>
          <QueryView query={reconciliation}>
            {(rows) => (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Method</TableCell>
                    <TableCell>Gateway</TableCell>
                    <TableCell align="right">Payments</TableCell>
                    <TableCell align="right">Collected</TableCell>
                    <TableCell align="right">Refunded</TableCell>
                    <TableCell align="right">Net</TableCell>
                    <TableCell align="right">Failed</TableCell>
                    <TableCell align="right">Pending</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {rows.map((r) => (
                    <TableRow key={`${r.paymentMethodId}-${r.gatewayName}`}>
                      <TableCell>{r.paymentMethodName}</TableCell>
                      <TableCell>{r.gatewayName}</TableCell>
                      <TableCell align="right">{r.paymentCount}</TableCell>
                      <TableCell align="right">{formatMoney(r.collectedAmount)}</TableCell>
                      <TableCell align="right">{formatMoney(r.refundedAmount)}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 700 }}>{formatMoney(r.netAmount)}</TableCell>
                      <TableCell align="right">{r.failedCount}</TableCell>
                      <TableCell align="right">{r.pendingCount}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </QueryView>
        </Section>
      )}
    </>
  );
}

export function PaymentDetailPage() {
  const id = Number(useParams().id);
  const { can } = useAuth();
  const query = useQuery({ queryKey: ['payment', id], queryFn: () => financeApi.payment(id) });
  const [refunding, setRefunding] = useState(false);
  const run = useRun();

  return (
    <QueryView query={query}>
      {({ payment: p, refunds }) => (
        <>
          <PageHeader
            back={back('/payments', 'Payments')}
            title={`Payment ${p.paymentNumber}`}
            subtitle={<StatusChip status={paymentStatus[p.paymentStatusId]} size="medium" />}
            actions={can(P.ManageRefunds) && (p.paymentStatusId === 4 || p.paymentStatusId === 7) && <Button color="error" onClick={() => setRefunding(true)}>Refund</Button>}
          />
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Details">
                <DetailList
                  items={[
                    ['Amount', formatMoney(p.amount)],
                    ['Refunded', formatMoney(p.refundedAmount)],
                    ['Customer', p.customerName],
                    ['Invoice', <Button component={RouterLink} to={`/invoices/${p.invoiceId}`} sx={{ p: 0, minWidth: 0 }}>{p.invoiceNumber}</Button>],
                    ['Method', paymentMethod[p.paymentMethodId]],
                    ['Gateway', p.gatewayName],
                    ['Gateway order', p.gatewayOrderId],
                    ['Gateway payment', p.gatewayTransactionId],
                    ['Reference', p.referenceNumber],
                    ['Paid at', formatDateTime(p.paymentDateUtc)],
                    ['Failure', p.failureReason],
                    ['Remarks', p.remarks],
                  ]}
                />
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Section title="Refunds">
                {refunds.length === 0 ? (
                  <Typography color="text.secondary">No refunds.</Typography>
                ) : (
                  <Stack spacing={1.25}>
                    {refunds.map((r) => (
                      <Box key={r.paymentRefundId}>
                        <Typography sx={{ fontWeight: 650 }}>{r.refundNumber}: {formatMoney(r.amount)} {r.refundStatusId === 2 ? '(processed)' : r.refundStatusId === 3 ? '(failed)' : '(pending)'}</Typography>
                        <Typography variant="body2" color="text.secondary">{r.reason}, {formatDateTime(r.processedDateUtc ?? r.createdDateUtc)}</Typography>
                        {r.failureReason && <Typography variant="body2" color="error">{r.failureReason}</Typography>}
                      </Box>
                    ))}
                  </Stack>
                )}
              </Section>
            </Grid>
          </Grid>
          {refunding && (
            <RecordDialog
              title="Refund this payment"
              submitLabel="Refund"
              fields={[
                { name: 'amount', label: 'Amount (₹)', type: 'number', required: true, helper: `Up to ${formatMoney(p.amount - p.refundedAmount)}` },
                { name: 'reason', label: 'Reason', required: true, type: 'multiline' },
              ]}
              initial={{ amount: p.amount - p.refundedAmount, reason: '' }}
              onClose={() => setRefunding(false)}
              onSubmit={async (v) => {
                if (await run(() => financeApi.refund(id, Number(v.amount), String(v.reason)), 'Refund processed', [['payment', id]])) setRefunding(false);
              }}
            />
          )}
        </>
      )}
    </QueryView>
  );
}

// ---------------- settlements ----------------

export function SettlementsPage() {
  const navigate = useNavigate();
  const { can } = useAuth();
  const [tab, setTab] = useState(0);
  const [status, setStatus] = useUrlFilter('status');
  const notify = useNotify();

  return (
    <>
      <PageHeader title="Owner settlements" subtitle="Completed, fully paid trips are settled to the vehicle owner. Approval must come from a different person than the one who prepared the settlement." />
      <Tabs value={tab} onChange={(_, t) => setTab(t)} sx={{ mb: 2 }}>
        <Tab label="Settlements" />
        {can(P.ManageSettlements) && <Tab label="Ready to settle" />}
      </Tabs>
      {tab === 0 ? (
        <DataTable<SettlementListItem>
          queryKey={['settlements', status]}
          fetcher={(q) => financeApi.settlements({ ...q, status: status ? Number(status) : undefined })}
          rowKey={(s) => s.settlementId}
          onRowClick={(s) => navigate(`/settlements/${s.settlementId}`)}
          searchPlaceholder="Settlement, trip or owner"
          filters={<FilterSelect label="Status" value={status} onChange={setStatus} options={statusOptions(settlementStatus)} />}
          empty={{ title: 'No settlements match' }}
          columns={[
            { header: 'Settlement', primary: true, render: (s) => <Typography sx={{ fontWeight: 650 }}>{s.settlementNumber}</Typography> },
            { header: 'Owner', primary: true, render: (s) => s.ownerName },
            { header: 'Trip', render: (s) => <PlateTag size="small">{s.tripNumber}</PlateTag> },
            { header: 'Freight', align: 'right', render: (s) => formatMoney(s.grossAmount) },
            { header: 'Commission', align: 'right', render: (s) => formatMoney(s.commissionAmount) },
            { header: 'Net payout', primary: true, align: 'right', render: (s) => formatMoney(s.netAmount) },
            { header: 'Status', primary: true, render: (s) => <StatusChip status={settlementStatus[s.settlementStatusId]} /> },
          ]}
        />
      ) : (
        <DataTable<EligibleTrip>
          queryKey={['eligible-trips']}
          fetcher={(q) => financeApi.eligibleTrips(q)}
          rowKey={(t) => t.tripId}
          empty={{ title: 'Nothing to settle', text: 'Trips appear here once they are completed and the customer has paid the invoice in full.' }}
          columns={[
            { header: 'Trip', primary: true, render: (t) => <PlateTag size="small">{t.tripNumber}</PlateTag> },
            { header: 'Booking', render: (t) => t.bookingNumber },
            { header: 'Owner', primary: true, render: (t) => t.ownerName },
            { header: 'Delivered', render: (t) => formatDate(t.actualDeliveryDateUtc) },
            { header: 'Freight (taxable)', align: 'right', render: (t) => formatMoney(t.invoiceSubTotal) },
            {
              header: '',
              primary: true,
              render: (t) => (
                <Button
                  size="small"
                  variant="contained"
                  onClick={async (e) => {
                    e.stopPropagation();
                    try {
                      const created = await financeApi.createSettlement(t.tripId);
                      navigate(`/settlements/${created.id}`);
                    } catch (err) {
                      notify(toApiError(err).message, 'error');
                    }
                  }}
                >
                  Prepare settlement
                </Button>
              ),
            },
          ]}
        />
      )}
    </>
  );
}

type SettlementDialog = 'adjust' | 'complete' | 'fail' | 'cancel';

export function SettlementDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['settlement', id], queryFn: () => financeApi.settlement(id) });
  const dialog = useDialog<SettlementDialog>();
  const run = useRun();
  const keys = [['settlement', id], ['settlements']];

  return (
    <QueryView query={query}>
      {({ settlement: s, items, availableActions: a }) => (
        <>
          <PageHeader
            back={back('/settlements', 'Settlements')}
            title={`Settlement ${s.settlementNumber}`}
            subtitle={<StatusChip status={settlementStatus[s.settlementStatusId]} size="medium" />}
            actions={
              <>
                {a.includes('Approve') && <Button variant="contained" onClick={() => run(() => financeApi.settlementAction(id, 'approve'), 'Settlement approved', keys)}>Approve</Button>}
                {a.includes('StartProcessing') && <Button variant="contained" onClick={() => run(() => financeApi.settlementAction(id, 'process'), 'Payout started', keys)}>Start payout</Button>}
                {a.includes('Complete') && <Button variant="contained" onClick={() => dialog.show('complete')}>Mark as paid</Button>}
                {a.includes('Fail') && <Button color="error" onClick={() => dialog.show('fail')}>Payout failed</Button>}
                {a.includes('Adjust') && <Button onClick={() => dialog.show('adjust')}>Add adjustment</Button>}
                {a.includes('Cancel') && <Button color="error" onClick={() => dialog.show('cancel')}>Cancel</Button>}
              </>
            }
          />
          {s.settlementStatusId === 1 && !a.includes('Approve') && (
            <Alert severity="info" sx={{ mb: 2 }}>Waiting for approval by a second finance user.</Alert>
          )}
          {s.failureReason && <Alert severity="error" sx={{ mb: 2 }}>{s.failureReason}</Alert>}
          <Grid container spacing={2.5}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Section title="Statement">
                <Table size="small">
                  <TableBody>
                    {items.map((i) => (
                      <TableRow key={i.settlementItemId}>
                        <TableCell sx={{ pl: 0 }}>{i.description}</TableCell>
                        <TableCell align="right" sx={{ pr: 0 }}>{formatMoney(i.amount)}</TableCell>
                      </TableRow>
                    ))}
                    <TableRow>
                      <TableCell sx={{ pl: 0, fontWeight: 800 }}>Net payout</TableCell>
                      <TableCell align="right" sx={{ pr: 0, fontWeight: 800 }}>{formatMoney(s.netAmount)}</TableCell>
                    </TableRow>
                  </TableBody>
                </Table>
              </Section>
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Section title="Details">
                <DetailList
                  columns={1}
                  items={[
                    ['Owner', <Button component={RouterLink} to={`/owners/${s.ownerId}`} sx={{ p: 0, minWidth: 0 }}>{s.ownerName}</Button>],
                    ['Trip', <Button component={RouterLink} to={`/trips/${s.tripId}`} sx={{ p: 0, minWidth: 0 }}>{s.tripNumber}</Button>],
                    ['Booking', s.bookingNumber],
                    ['Commission', `${s.commissionPercent}% (${formatMoney(s.commissionAmount)})`],
                    ['Pay to', s.bankName ? `${s.bankName} ••••${s.accountNumberLast4}` : 'Primary account at payout'],
                    ['Approved', formatDateTime(s.approvedDateUtc)],
                    ['Paid', formatDateTime(s.settlementDateUtc)],
                    ['Bank reference', s.transactionReference],
                  ]}
                />
              </Section>
            </Grid>
          </Grid>

          {dialog.is('adjust') && (
            <RecordDialog
              title="Add an adjustment"
              fields={[
                { name: 'description', label: 'Description', required: true, helper: 'e.g. Damage deduction for booking PC-BKG-…' },
                { name: 'amount', label: 'Amount (₹, negative to deduct)', type: 'number', required: true },
              ]}
              initial={{ description: '', amount: '' }}
              onClose={dialog.close}
              onSubmit={async (v) => {
                if (await run(() => financeApi.adjustSettlement(id, String(v.description), Number(v.amount)), 'Adjustment added', keys)) dialog.close();
              }}
            />
          )}
          {dialog.is('complete') && (
            <RecordDialog
              title="Mark payout as paid"
              submitLabel="Mark as paid"
              fields={[{ name: 'transactionReference', label: 'Bank UTR / transaction reference', required: true }]}
              initial={{ transactionReference: '' }}
              onClose={dialog.close}
              onSubmit={async (v) => {
                if (await run(() => financeApi.completeSettlement(id, String(v.transactionReference)), 'Settlement paid; trip and booking closed', keys)) dialog.close();
              }}
            />
          )}
          <ReasonDialog open={dialog.is('fail')} title="Record a failed payout" confirmLabel="Mark as failed" destructive onClose={dialog.close} onSubmit={(r) => run(() => financeApi.failSettlement(id, r), 'Payout marked as failed', keys)} />
          <ReasonDialog open={dialog.is('cancel')} title="Cancel this settlement?" confirmLabel="Cancel settlement" destructive onClose={dialog.close} onSubmit={(r) => run(() => financeApi.cancelSettlement(id, r), 'Settlement cancelled', keys)} />
        </>
      )}
    </QueryView>
  );
}
