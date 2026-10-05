import { describe, expect, it } from 'vitest';
import { formatDateTime, formatMoney, fromLocalInput, toLocalInput } from '../lib/format';
import { bookingJourney, bookingStatus } from '../lib/statuses';
import * as v from '../lib/validation';

describe('formatting', () => {
  it('formats rupees with Indian digit grouping', () => {
    expect(formatMoney(123456.5)).toBe('₹1,23,456.50');
  });

  it('shows UTC instants in IST', () => {
    expect(formatDateTime('2026-10-05T04:30:00Z')).toBe('5 Oct 2026, 10:00 AM');
  });

  it('round-trips datetime-local values through IST', () => {
    const utc = '2026-10-05T04:30:00.000Z';
    expect(toLocalInput(utc)).toBe('2026-10-05T10:00');
    expect(fromLocalInput('2026-10-05T10:00')).toBe(utc);
  });
});

describe('validation', () => {
  it('accepts Indian mobile numbers in common formats', () => {
    for (const phone of ['9845012345', '+91 98450 12345', '+919845012345']) {
      expect(v.phone.safeParse(phone).success).toBe(true);
    }
    expect(v.phone.safeParse('12345').success).toBe(false);
  });

  it('normalizes and validates vehicle numbers', () => {
    expect(v.vehicleNumber.parse('ka 25 ab 1234')).toBe('KA25AB1234');
    expect(v.vehicleNumber.safeParse('22 BH 1234 AA').success).toBe(true);
    expect(v.vehicleNumber.safeParse('XYZ').success).toBe(false);
  });

  it('requires strong passwords', () => {
    expect(v.password.safeParse('ProCargo@Dev1').success).toBe(true);
    expect(v.password.safeParse('password').success).toBe(false);
  });

  it('rejects markup in free text', () => {
    expect(v.safeText(100).safeParse('<script>').success).toBe(false);
    expect(v.safeText(100).safeParse('Handle with care').success).toBe(true);
  });

  it('validates GSTIN, IFSC and PIN codes', () => {
    expect(v.gst.safeParse('29ABCDE1234F1Z5').success).toBe(true);
    expect(v.gst.safeParse('').success).toBe(true);
    expect(v.gst.safeParse('29ABCDE').success).toBe(false);
    expect(v.ifsc.safeParse('SBIN0001234').success).toBe(true);
    expect(v.pincode.safeParse('560058').success).toBe(true);
    expect(v.pincode.safeParse('060058').success).toBe(false);
  });
});

describe('statuses', () => {
  it('covers every booking status id', () => {
    for (let id = 1; id <= 14; id++) expect(bookingStatus[id]).toBeDefined();
  });

  it('marks a delivered booking as past the transit stage', () => {
    const reached = bookingJourney.filter((s) => s.reachedAt.includes(8)).map((s) => s.label);
    expect(reached).toContain('In transit');
    expect(reached).not.toContain('Paid');
  });
});
