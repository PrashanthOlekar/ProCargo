import dayjs from 'dayjs';
import utc from 'dayjs/plugin/utc';
import relativeTime from 'dayjs/plugin/relativeTime';

dayjs.extend(utc);
dayjs.extend(relativeTime);

/** India has no daylight saving: IST is always UTC+05:30. Every date from the API is UTC. */
const IST_OFFSET_MINUTES = 330;

const inr = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 });
const number = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 });

export const formatMoney = (value: number | null | undefined) => (value == null ? '—' : inr.format(value));
export const formatNumber = (value: number | null | undefined) => (value == null ? '—' : number.format(value));
export const formatKg = (value: number | null | undefined) => (value == null ? '—' : `${number.format(value)} kg`);
export const formatKm = (value: number | null | undefined) => (value == null ? '—' : `${number.format(value)} km`);

export const toIst = (utcValue: string) => dayjs.utc(utcValue).utcOffset(IST_OFFSET_MINUTES);

export const formatDateTime = (value: string | null | undefined) => (value ? toIst(value).format('D MMM YYYY, h:mm A') : '—');
export const formatDate = (value: string | null | undefined) => (value ? toIst(value).format('D MMM YYYY') : '—');
/** DateOnly values ("2026-10-05") have no time zone. */
export const formatDateOnly = (value: string | null | undefined) => (value ? dayjs(value).format('D MMM YYYY') : '—');
export const fromNow = (value: string) => dayjs.utc(value).fromNow();

/** Value for <input type="datetime-local"> in IST from a UTC instant, and back. */
export const toLocalInput = (utcValue: string) => toIst(utcValue).format('YYYY-MM-DDTHH:mm');
export const fromLocalInput = (local: string) => dayjs.utc(local).subtract(IST_OFFSET_MINUTES, 'minute').toISOString();

export const daysUntil = (dateOnly: string | null | undefined) =>
  dateOnly ? dayjs(dateOnly).startOf('day').diff(dayjs().startOf('day'), 'day') : null;

export const fileSize = (bytes: number) =>
  bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
