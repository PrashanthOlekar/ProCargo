/**
 * Status vocabularies. Ids mirror the mst.* lookup tables; names are what customers and partners read.
 * Tone drives the chip colour: neutral (waiting on someone), progress (moving), good (done), bad (stopped).
 */
export type Tone = 'neutral' | 'progress' | 'good' | 'bad' | 'warn';

export interface StatusInfo {
  label: string;
  tone: Tone;
}

const map = (entries: [number, string, Tone][]) =>
  Object.fromEntries(entries.map(([id, label, tone]) => [id, { label, tone }])) as Record<number, StatusInfo>;

export const bookingStatus = map([
  [1, 'Draft', 'neutral'],
  [2, 'Submitted', 'neutral'],
  [3, 'Under review', 'neutral'],
  [4, 'Quoted', 'warn'],
  [5, 'Confirmed', 'progress'],
  [6, 'Truck assigned', 'progress'],
  [7, 'In transit', 'progress'],
  [8, 'Delivered', 'good'],
  [9, 'Invoiced', 'good'],
  [10, 'Paid', 'good'],
  [11, 'Closed', 'good'],
  [12, 'Cancelled', 'bad'],
  [13, 'On hold', 'warn'],
  [14, 'Rejected', 'bad'],
]);

export const tripStatus = map([
  [1, 'Scheduled', 'neutral'],
  [2, 'Picked up', 'progress'],
  [3, 'In transit', 'progress'],
  [4, 'Delivered', 'good'],
  [5, 'POD uploaded', 'good'],
  [6, 'Completed', 'good'],
  [7, 'Closed', 'good'],
  [8, 'Cancelled', 'bad'],
  [9, 'On hold', 'warn'],
  [10, 'Exception', 'bad'],
]);

export const quotationStatus = map([
  [1, 'Draft', 'neutral'],
  [2, 'Sent to customer', 'warn'],
  [3, 'Accepted', 'good'],
  [4, 'Rejected', 'bad'],
  [5, 'Expired', 'bad'],
  [6, 'Withdrawn', 'bad'],
]);

export const invoiceStatus = map([
  [1, 'Draft', 'neutral'],
  [2, 'Unpaid', 'warn'],
  [3, 'Partly paid', 'warn'],
  [4, 'Paid', 'good'],
  [5, 'Cancelled', 'bad'],
]);

export const paymentStatus = map([
  [1, 'Pending', 'neutral'],
  [2, 'Started', 'neutral'],
  [3, 'Authorised', 'progress'],
  [4, 'Paid', 'good'],
  [5, 'Failed', 'bad'],
  [6, 'Refunded', 'neutral'],
  [7, 'Partly refunded', 'neutral'],
  [8, 'Partly paid', 'warn'],
  [9, 'Cancelled', 'bad'],
]);

export const settlementStatus = map([
  [1, 'Pending approval', 'warn'],
  [2, 'Approved', 'progress'],
  [3, 'Payout in progress', 'progress'],
  [4, 'Paid out', 'good'],
  [5, 'Payout failed', 'bad'],
  [6, 'Cancelled', 'bad'],
]);

export const verificationStatus = map([
  [1, 'Not verified', 'neutral'],
  [2, 'Under review', 'warn'],
  [3, 'Verified', 'good'],
  [4, 'Rejected', 'bad'],
  [5, 'Expired', 'bad'],
]);

export const availabilityStatus = map([
  [1, 'Available', 'good'],
  [2, 'On a trip', 'progress'],
  [3, 'Off duty', 'neutral'],
  [4, 'Unavailable', 'bad'],
]);

export const ticketStatus = map([
  [1, 'Open', 'warn'],
  [2, 'In progress', 'progress'],
  [3, 'Waiting on customer', 'warn'],
  [4, 'Resolved', 'good'],
  [5, 'Closed', 'neutral'],
]);

export const complaintStatus = map([
  [1, 'Open', 'warn'],
  [2, 'Assigned', 'progress'],
  [3, 'Investigating', 'progress'],
  [4, 'Resolved', 'good'],
  [5, 'Rejected', 'bad'],
  [6, 'Closed', 'neutral'],
]);

export const ticketPriority: Record<number, string> = { 1: 'Low', 2: 'Medium', 3: 'High', 4: 'Critical' };

export const paymentMethod: Record<number, string> = {
  1: 'UPI',
  2: 'Card',
  3: 'Net banking',
  4: 'Wallet',
  5: 'Bank transfer',
  6: 'Cash',
  7: 'Cheque',
};

/** The stages a customer sees on a booking, in order. */
export const bookingJourney: { label: string; reachedAt: number[] }[] = [
  { label: 'Requested', reachedAt: [2, 3, 4, 5, 6, 7, 8, 9, 10, 11] },
  { label: 'Quoted', reachedAt: [4, 5, 6, 7, 8, 9, 10, 11] },
  { label: 'Confirmed', reachedAt: [5, 6, 7, 8, 9, 10, 11] },
  { label: 'Truck assigned', reachedAt: [6, 7, 8, 9, 10, 11] },
  { label: 'In transit', reachedAt: [7, 8, 9, 10, 11] },
  { label: 'Delivered', reachedAt: [8, 9, 10, 11] },
  { label: 'Paid', reachedAt: [10, 11] },
];
