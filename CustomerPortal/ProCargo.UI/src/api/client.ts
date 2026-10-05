import axios, { AxiosError, type AxiosRequestConfig, type InternalAxiosRequestConfig } from 'axios';
import type { AuthResponse } from './types';

/** Portal this SPA signs in to. The Operations portal has its own SPA and its own refresh cookie. */
export const PORTAL = 'Web' as const;

const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1';

/**
 * The access token lives only in memory (never localStorage), so an XSS payload cannot lift a long-lived
 * credential. The refresh token is an HttpOnly cookie the browser sends to /api/v1/auth only.
 */
let accessToken: string | null = null;
let refreshInFlight: Promise<AuthResponse | null> | null = null;
const listeners = new Set<(auth: AuthResponse | null) => void>();

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onSessionChange(listener: (auth: AuthResponse | null) => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export const api = axios.create({
  baseURL,
  withCredentials: true,
  timeout: 30_000,
  headers: { 'X-ProCargo-Client': 'procargo-web' },
});

api.interceptors.request.use((config) => {
  if (accessToken) config.headers.set('Authorization', `Bearer ${accessToken}`);
  return config;
});

/** Single-flight refresh: concurrent 401s wait for one refresh call instead of each rotating the token. */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!refreshInFlight) {
    refreshInFlight = axios
      .post<AuthResponse>(`${baseURL}/auth/refresh`, { portal: PORTAL }, {
        withCredentials: true,
        headers: { 'X-ProCargo-Client': 'procargo-web' },
      })
      .then((r) => {
        setAccessToken(r.data.accessToken);
        listeners.forEach((l) => l(r.data));
        return r.data;
      })
      .catch(() => {
        setAccessToken(null);
        listeners.forEach((l) => l(null));
        return null;
      })
      .finally(() => {
        refreshInFlight = null;
      });
  }
  return refreshInFlight;
}

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined;
    const isAuthCall = config?.url?.startsWith('/auth/');
    if (error.response?.status === 401 && config && !config._retried && !isAuthCall) {
      config._retried = true;
      const session = await refreshSession();
      if (session) return api(config as AxiosRequestConfig);
    }
    return Promise.reject(toApiError(error));
  },
);

/** Error body returned by the API for every non-2xx response. */
export interface ApiErrorBody {
  success: false;
  message: string;
  errorCode: string;
  errors?: Record<string, string[]> | null;
  traceId?: string | null;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly traceId?: string | null;

  constructor(status: number, body: Partial<ApiErrorBody>) {
    super(body.message ?? 'Something went wrong. Please try again.');
    this.status = status;
    this.code = body.errorCode ?? 'UNKNOWN';
    this.fieldErrors = body.errors ?? {};
    this.traceId = body.traceId;
  }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error;
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return new ApiError(0, { message: 'Cannot reach ProCargo. Check your internet connection and try again.', errorCode: 'NETWORK' });
    }
    const data = error.response.data as Partial<ApiErrorBody> | undefined;
    if (error.response.status === 429) {
      return new ApiError(429, { message: 'Too many attempts. Wait a minute and try again.', errorCode: 'RATE_LIMITED' });
    }
    return new ApiError(error.response.status, data ?? {});
  }
  return new ApiError(0, { message: (error as Error)?.message });
}

/** Downloads a protected file through the API (auth header attached) and saves it. */
export async function downloadFile(url: string, fallbackName: string) {
  const response = await api.get<Blob>(url, { responseType: 'blob' });
  const disposition = response.headers['content-disposition'] as string | undefined;
  const match = disposition?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const href = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = href;
  link.download = name;
  link.click();
  URL.revokeObjectURL(href);
}

/** Opens a protected image/PDF in a new tab. */
export async function openFile(url: string) {
  const response = await api.get<Blob>(url, { responseType: 'blob' });
  const href = URL.createObjectURL(response.data);
  window.open(href, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(href), 60_000);
}

export function newIdempotencyKey() {
  return crypto.randomUUID();
}
