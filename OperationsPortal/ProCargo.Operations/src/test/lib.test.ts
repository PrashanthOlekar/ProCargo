import { describe, expect, it } from 'vitest';
import { formatDateTime, formatMoney, fromLocalInput, toLocalInput } from '../lib/format';
import {
  bookingStatus,
  complaintStatus,
  invoiceStatus,
  paymentStatus,
  quotationStatus,
  settlementStatus,
  ticketStatus,
  tripStatus,
  verificationStatus,
} from '../lib/statuses';

describe('formatting', () => {
  it('formats rupees with Indian digit grouping', () => {
    expect(formatMoney(1234567)).toBe('₹12,34,567.00');
  });

  it('shows UTC instants in IST and round-trips datetime-local values', () => {
    expect(formatDateTime('2026-10-05T04:30:00Z')).toBe('5 Oct 2026, 10:00 AM');
    expect(fromLocalInput(toLocalInput('2026-10-05T04:30:00.000Z'))).toBe('2026-10-05T04:30:00.000Z');
  });
});

describe('statuses match the database lookup ids', () => {
  const cases: [string, Record<number, unknown>, number][] = [
    ['booking', bookingStatus, 14],
    ['trip', tripStatus, 10],
    ['quotation', quotationStatus, 6],
    ['payment', paymentStatus, 9],
    ['invoice', invoiceStatus, 5],
    ['settlement', settlementStatus, 6],
    ['verification', verificationStatus, 5],
    ['ticket', ticketStatus, 5],
    ['complaint', complaintStatus, 6],
  ];
  it.each(cases)('%s statuses cover ids 1..n', (_name, map, count) => {
    for (let id = 1; id <= count; id++) expect(map[id]).toBeDefined();
    expect(map[count + 1]).toBeUndefined();
  });
});
