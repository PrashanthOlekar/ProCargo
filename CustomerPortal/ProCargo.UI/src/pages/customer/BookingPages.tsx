import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Divider, Grid, Link, Stack, Table, TableBody, TableCell, TableRow, Typography } from '@mui/material';
import ArrowBack from '@mui/icons-material/ArrowBack';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { bookingApi, quotationApi, tripApi } from '../../api/endpoints';
import type { BookingListItem } from '../../api/types';
import { DataTable } from '../../components/DataTable';
import { ConfirmDialog, ReasonDialog, useNotify } from '../../components/Forms';
import { DetailList, PageHeader, QueryView, Section } from '../../components/Layout';
import { PlateTag, StatusChip } from '../../components/PlateTag';
import { JourneyBar, StatusTimeline } from '../../components/Timeline';
import { TripTrackingPanel } from '../../components/TripTracking';
import { formatDate, formatDateTime, formatKg, formatKm, formatMoney } from '../../lib/format';
import { bookingJourney, bookingStatus, quotationStatus, tripStatus } from '../../lib/statuses';
import { toApiError } from '../../api/client';

export function BookingsPage() {
  const navigate = useNavigate();
  return (
    <>
      <PageHeader
        title="My bookings"
        actions={
          <Button component={RouterLink} to="/customer/bookings/new" variant="contained">
            Book a lorry
          </Button>
        }
      />
      <DataTable<BookingListItem>
        queryKey={['bookings']}
        fetcher={(q) => bookingApi.list(q)}
        rowKey={(b) => b.bookingId}
        onRowClick={(b) => navigate(`/customer/bookings/${b.bookingId}`)}
        searchPlaceholder="Search by booking number"
        empty={{
          title: 'No bookings yet',
          text: 'Tell us what to move and where. You get a fixed-price quotation before anything is confirmed.',
          action: (
            <Button component={RouterLink} to="/customer/bookings/new" variant="contained">
              Book a lorry
            </Button>
          ),
        }}
        columns={[
          { header: 'Booking', primary: true, render: (b) => <PlateTag size="small">{b.bookingNumber}</PlateTag> },
          { header: 'Route', primary: true, render: (b) => `${b.pickupCityName} to ${b.deliveryCityName}` },
          { header: 'Vehicle', render: (b) => b.vehicleTypeName },
          { header: 'Load', render: (b) => formatKg(b.totalWeightKg) },
          { header: 'Pickup', primary: true, render: (b) => formatDate(b.requestedPickupDateUtc) },
          { header: 'Status', primary: true, render: (b) => <StatusChip status={bookingStatus[b.bookingStatusId]} /> },
        ]}
      />
    </>
  );
}

export function BookingDetailPage() {
  const id = Number(useParams().id);
  const query = useQuery({ queryKey: ['booking', id], queryFn: () => bookingApi.get(id) });
  const history = useQuery({ queryKey: ['booking', id, 'history'], queryFn: () => bookingApi.history(id) });
  const quotations = useQuery({ queryKey: ['booking', id, 'quotations'], queryFn: () => quotationApi.list({ bookingId: id, pageSize: 10 }) });
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [cancelOpen, setCancelOpen] = useState(false);
  const [submitBusy, setSubmitBusy] = useState(false);

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['booking', id] });

  return (
    <QueryView query={query}>
      {({ booking: b, items, availableActions }) => {
        const openQuotation = quotations.data?.items.find((q) => q.quotationStatusId === 2);
        return (
          <>
            <PageHeader
              back={
                <Button component={RouterLink} to="/customer/bookings" startIcon={<ArrowBack />} sx={{ mb: 1, ml: -1 }}>
                  My bookings
                </Button>
              }
              title={
                <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
                  <span>
                    {b.pickupCityName} to {b.deliveryCityName}
                  </span>
                  <PlateTag>{b.bookingNumber}</PlateTag>
                </Stack>
              }
              subtitle={<StatusChip status={bookingStatus[b.bookingStatusId]} size="medium" />}
              actions={
                <>
                  {availableActions.includes('Edit') && (
                    <Button component={RouterLink} to={`/customer/bookings/${id}/edit`}>
                      Edit
                    </Button>
                  )}
                  {availableActions.includes('Submit') && (
                    <Button
                      variant="contained"
                      disabled={submitBusy}
                      onClick={async () => {
                        setSubmitBusy(true);
                        try {
                          await bookingApi.submit(id);
                          notify('Sent for a quotation');
                          refresh();
                        } catch (e) {
                          notify(toApiError(e).message, 'error');
                        } finally {
                          setSubmitBusy(false);
                        }
                      }}
                    >
                      Request a quotation
                    </Button>
                  )}
                  {availableActions.includes('Cancel') && (
                    <Button color="error" onClick={() => setCancelOpen(true)}>
                      Cancel booking
                    </Button>
                  )}
                </>
              }
            />

            {b.bookingStatusId !== 12 && b.bookingStatusId !== 14 && (
              <Box sx={{ mb: 3 }}>
                <JourneyBar statusId={b.bookingStatusId} stages={bookingJourney} />
              </Box>
            )}
            {b.cancellationReason && (
              <Alert severity="info" sx={{ mb: 2 }}>
                {b.bookingStatusId === 14 ? 'Not accepted' : 'Cancelled'}: {b.cancellationReason}
              </Alert>
            )}

            {openQuotation && <QuotationDecision quotationId={openQuotation.quotationId} onDone={refresh} />}

            <Grid container spacing={2.5}>
              <Grid size={{ xs: 12, md: 7 }}>
                {b.tripId && (
                  <Section title="Your truck" action={b.tripStatusId ? <StatusChip status={tripStatus[b.tripStatusId]} /> : undefined}>
                    <DetailList
                      items={[
                        ['Vehicle', b.assignedVehicleNumber ? <PlateTag size="small">{b.assignedVehicleNumber}</PlateTag> : '—'],
                        ['Driver', b.assignedDriverName],
                        ['Driver mobile', b.assignedDriverPhone ? <Link href={`tel:${b.assignedDriverPhone}`}>{b.assignedDriverPhone}</Link> : '—'],
                        ['Trip', b.tripNumber],
                      ]}
                    />
                    <Divider sx={{ my: 2.5 }} />
                    <TripTrackingPanel
                      tripId={b.tripId}
                      active={b.tripStatusId === 2 || b.tripStatusId === 3}
                      pickup={b.pickupLatitude && b.pickupLongitude ? { latitude: b.pickupLatitude, longitude: b.pickupLongitude } : null}
                      delivery={b.deliveryLatitude && b.deliveryLongitude ? { latitude: b.deliveryLatitude, longitude: b.deliveryLongitude } : null}
                    />
                    {b.tripStatusId && b.tripStatusId >= 5 && b.tripStatusId <= 7 && <ProofOfDeliverySummary tripId={b.tripId} />}
                  </Section>
                )}

                <Section title="Route">
                  <Grid container spacing={3}>
                    {[
                      ['Pickup', b.pickupAddressLine1, b.pickupAddressLine2, b.pickupLandmark, b.pickupCityName, b.pickupPincode, b.pickupContactName, b.pickupContactPhone],
                      ['Delivery', b.deliveryAddressLine1, b.deliveryAddressLine2, b.deliveryLandmark, b.deliveryCityName, b.deliveryPincode, b.deliveryContactName, b.deliveryContactPhone],
                    ].map(([label, l1, l2, landmark, city, pin, name, phone]) => (
                      <Grid key={label as string} size={{ xs: 12, sm: 6 }}>
                        <Typography variant="body2" color="text.secondary">
                          {label}
                        </Typography>
                        <Typography sx={{ fontWeight: 600 }}>{l1}</Typography>
                        {l2 && <Typography>{l2}</Typography>}
                        {landmark && <Typography color="text.secondary">Near {landmark}</Typography>}
                        <Typography>
                          {city} {pin}
                        </Typography>
                        <Typography variant="body2" sx={{ mt: 1 }}>
                          {name}, {phone}
                        </Typography>
                      </Grid>
                    ))}
                  </Grid>
                </Section>

                <Section title="Goods">
                  <DetailList
                    items={[
                      ['Goods', `${b.goodsTypeName} — ${b.goodsDescription}`],
                      ['Vehicle requested', b.vehicleTypeName],
                      ['Total load', `${formatKg(b.totalWeightKg)} in ${b.totalQuantity} pieces`],
                      ['Pickup', formatDateTime(b.requestedPickupDateUtc)],
                      ['Estimated distance', formatKm(b.estimatedDistanceKm)],
                      ['Instructions', b.specialInstructions],
                    ]}
                  />
                  <Table size="small" sx={{ mt: 2 }}>
                    <TableBody>
                      {items.map((i) => (
                        <TableRow key={i.bookingItemId}>
                          <TableCell>
                            {i.description}
                            {i.isFragile ? ' (fragile)' : ''}
                          </TableCell>
                          <TableCell align="right">{i.quantity} pcs</TableCell>
                          <TableCell align="right">{formatKg(i.weightKg)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Section>
              </Grid>

              <Grid size={{ xs: 12, md: 5 }}>
                {b.acceptedQuotationAmount != null && (
                  <Section title="Agreed price">
                    <Typography sx={{ fontSize: '2rem', fontWeight: 800, fontStretch: '80%' }}>{formatMoney(b.acceptedQuotationAmount)}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      Including GST. This is the amount on your invoice.
                    </Typography>
                    {b.invoiceId && (
                      <Button component={RouterLink} to={`/customer/invoices/${b.invoiceId}`} variant="contained" sx={{ mt: 2 }}>
                        View invoice
                      </Button>
                    )}
                  </Section>
                )}
                {(quotations.data?.items.length ?? 0) > 0 && (
                  <Section title="Quotations">
                    <Stack spacing={1.25}>
                      {quotations.data!.items.map((q) => (
                        <Stack key={q.quotationId} direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                          <Box>
                            <Typography sx={{ fontWeight: 600 }}>{formatMoney(q.totalAmount)}</Typography>
                            <Typography variant="body2" color="text.secondary">
                              {q.quotationNumber}, valid till {formatDateTime(q.validityDateUtc)}
                            </Typography>
                          </Box>
                          <StatusChip status={quotationStatus[q.quotationStatusId]} />
                        </Stack>
                      ))}
                    </Stack>
                  </Section>
                )}
                <Section title="History">
                  {history.data ? <StatusTimeline history={history.data} statuses={bookingStatus} /> : null}
                </Section>
              </Grid>
            </Grid>

            <ReasonDialog
              open={cancelOpen}
              title="Cancel this booking?"
              description="You can cancel free of charge until the truck picks up your goods."
              confirmLabel="Cancel booking"
              destructive
              onClose={() => setCancelOpen(false)}
              onSubmit={async (reason) => {
                await bookingApi.cancel(id, reason);
                notify('Booking cancelled');
                refresh();
              }}
            />
          </>
        );
      }}
    </QueryView>
  );
}

/** The open quotation with its full price breakdown and the accept / reject decision. */
function QuotationDecision({ quotationId, onDone }: { quotationId: number; onDone: () => void }) {
  const query = useQuery({ queryKey: ['quotation', quotationId], queryFn: () => quotationApi.get(quotationId) });
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [acceptOpen, setAcceptOpen] = useState(false);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const done = () => {
    queryClient.invalidateQueries({ queryKey: ['quotation', quotationId] });
    queryClient.invalidateQueries({ queryKey: ['booking'] });
    onDone();
  };

  return (
    <QueryView query={query}>
      {({ quotation: q, charges, availableActions }) => (
        <Section title="Your quotation is ready">
          <Grid container spacing={3}>
            <Grid size={{ xs: 12, md: 7 }}>
              <Table size="small">
                <TableBody>
                  {charges.map((c) => (
                    <TableRow key={c.quotationChargeId}>
                      <TableCell sx={{ pl: 0 }}>{c.description}</TableCell>
                      <TableCell align="right" sx={{ pr: 0 }}>
                        {formatMoney(c.amount)}
                      </TableCell>
                    </TableRow>
                  ))}
                  <TableRow>
                    <TableCell sx={{ pl: 0 }}>GST {q.taxPercent}%</TableCell>
                    <TableCell align="right" sx={{ pr: 0 }}>
                      {formatMoney(q.taxAmount)}
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
              {q.notes && (
                <Typography variant="body2" color="text.secondary" sx={{ mt: 1.5 }}>
                  Note from ProCargo: {q.notes}
                </Typography>
              )}
            </Grid>
            <Grid size={{ xs: 12, md: 5 }}>
              <Typography color="text.secondary">Total for {formatKm(q.distanceKm)}</Typography>
              <PlateTag size="large" sx={{ my: 1 }}>{formatMoney(q.totalAmount)}</PlateTag>
              <Typography variant="body2" color="text.secondary">
                Valid until {formatDateTime(q.validityDateUtc)}
              </Typography>
              {availableActions.includes('Accept') ? (
                <Stack direction="row" spacing={1.5} sx={{ mt: 2.5 }}>
                  <Button variant="contained" size="large" onClick={() => setAcceptOpen(true)}>
                    Accept and confirm
                  </Button>
                  <Button size="large" onClick={() => setRejectOpen(true)}>
                    Decline
                  </Button>
                </Stack>
              ) : (
                <Alert severity="warning" sx={{ mt: 2 }}>
                  This quotation has expired. Our team will send a fresh one.
                </Alert>
              )}
            </Grid>
          </Grid>

          <ConfirmDialog
            open={acceptOpen}
            title={`Confirm the booking for ${formatMoney(q.totalAmount)}?`}
            body="We'll assign a verified truck and driver and share their details here. You pay against the invoice after delivery."
            confirmLabel="Accept and confirm"
            busy={busy}
            onClose={() => setAcceptOpen(false)}
            onConfirm={async () => {
              setBusy(true);
              try {
                await quotationApi.accept(quotationId);
                notify('Booking confirmed');
                setAcceptOpen(false);
                done();
              } catch (e) {
                notify(toApiError(e).message, 'error');
              } finally {
                setBusy(false);
              }
            }}
          />
          <ReasonDialog
            open={rejectOpen}
            title="Decline this quotation?"
            description="Tell us why — a different price, date or vehicle — and we'll come back with a revised quotation."
            confirmLabel="Decline quotation"
            onClose={() => setRejectOpen(false)}
            onSubmit={async (reason) => {
              await quotationApi.reject(quotationId, reason);
              notify('Quotation declined');
              done();
            }}
          />
        </Section>
      )}
    </QueryView>
  );
}

function ProofOfDeliverySummary({ tripId }: { tripId: number }) {
  const pod = useQuery({ queryKey: ['trip', tripId, 'pod'], queryFn: () => tripApi.pod(tripId) });
  if (!pod.data) return null;
  const p = pod.data.proofOfDelivery;
  return (
    <Alert severity="success" sx={{ mt: 2.5 }}>
      Delivered to {p.receiverName} on {formatDateTime(p.deliveredDateUtc)}
      {pod.data.files.length > 0 ? ` with ${pod.data.files.length} photo${pod.data.files.length > 1 ? 's' : ''}` : ''}.
      {p.remarks ? ` "${p.remarks}"` : ''}
    </Alert>
  );
}
