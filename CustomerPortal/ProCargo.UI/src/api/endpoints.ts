import { api, newIdempotencyKey, PORTAL } from './client';
import type * as T from './types';

type Query = Record<string, string | number | boolean | null | undefined>;

const clean = (q?: Query) =>
  q ? Object.fromEntries(Object.entries(q).filter(([, v]) => v !== undefined && v !== null && v !== '')) : undefined;

const get = async <R>(url: string, params?: Query) => (await api.get<R>(url, { params: clean(params) })).data;
const post = async <R = void>(url: string, body?: unknown) => (await api.post<R>(url, body ?? {})).data;
const put = async <R = void>(url: string, body?: unknown) => (await api.put<R>(url, body ?? {})).data;
const del = async (url: string) => {
  await api.delete(url);
};

export const authApi = {
  login: (email: string, password: string) => post<T.AuthResponse>('/auth/login', { email, password, portal: PORTAL }),
  register: (body: unknown) => post<T.AuthResponse>('/auth/register', body),
  logout: () => post<T.MessageResponse>('/auth/logout', { portal: PORTAL }),
  forgotPassword: (email: string) => post<T.MessageResponse>('/auth/forgot-password', { email, portal: PORTAL }),
  resetPassword: (token: string, newPassword: string) => post<T.MessageResponse>('/auth/reset-password', { token, newPassword }),
  changePassword: (currentPassword: string, newPassword: string) =>
    post<T.MessageResponse>('/auth/change-password', { currentPassword, newPassword }),
  me: () => get<T.CurrentUser>('/auth/me'),
};

export const referenceApi = {
  reference: () => get<T.ReferenceData>('/master-data/reference'),
  cities: (stateId?: number) => get<T.City[]>('/master-data/cities', { stateId }),
  documentTypes: (appliesTo: string) => get<T.DocumentType[]>('/master-data/document-types', { appliesTo }),
  estimate: (body: unknown) => post<T.PriceEstimate>('/pricing/estimate', body),
  contact: (body: unknown) => post<T.MessageResponse>('/public/contact', body),
};

export const bookingApi = {
  list: (q: T.PageQuery & Query) => get<T.PagedResult<T.BookingListItem>>('/bookings', q),
  get: (id: number) => get<T.BookingDetailsResponse>(`/bookings/${id}`),
  create: (body: T.BookingInput & { submit: boolean }) => post<T.CreatedResponse>('/bookings', body),
  update: (id: number, body: T.BookingInput & { rowVersion: string }) => put(`/bookings/${id}`, body),
  submit: (id: number) => post(`/bookings/${id}/submit`),
  cancel: (id: number, reason: string) => post(`/bookings/${id}/cancel`, { reason }),
  history: (id: number) => get<T.StatusHistory[]>(`/bookings/${id}/history`),
};

export const quotationApi = {
  list: (q: T.PageQuery & Query) => get<T.PagedResult<T.QuotationListItem>>('/quotations', q),
  get: (id: number) => get<T.QuotationDetailsResponse>(`/quotations/${id}`),
  accept: (id: number) => post(`/quotations/${id}/accept`),
  reject: (id: number, reason: string) => post(`/quotations/${id}/reject`, { reason }),
};

export const tripApi = {
  list: (q: T.PageQuery & Query) => get<T.PagedResult<T.TripListItem>>('/trips', q),
  get: (id: number) => get<T.TripDetailsResponse>(`/trips/${id}`),
  history: (id: number) => get<T.StatusHistory[]>(`/trips/${id}/history`),
  tracking: (id: number, sinceUtc?: string) => get<T.TripTracking>(`/trips/${id}/tracking`, { sinceUtc }),
  sendOtp: (id: number, step: 'pickup' | 'delivery') => post<T.OtpSent>(`/trips/${id}/${step}/otp`),
  verifyOtp: (id: number, step: 'pickup' | 'delivery', body: unknown) => post(`/trips/${id}/${step}/verify`, body),
  start: (id: number, remarks?: string) => post(`/trips/${id}/start`, { remarks }),
  reportException: (id: number, reason: string) => post(`/trips/${id}/exception`, { reason }),
  addLocations: (id: number, points: unknown[]) => post<{ accepted: number }>(`/trips/${id}/locations`, { points }),
  uploadPod: (id: number, form: FormData) =>
    api.post<T.CreatedResponse>(`/trips/${id}/proof-of-delivery`, form, { headers: { 'Content-Type': 'multipart/form-data' } }).then((r) => r.data),
  pod: (id: number) => get<T.ProofOfDeliveryResponse>(`/trips/${id}/proof-of-delivery`),
};

export const financeApi = {
  invoices: (q: T.PageQuery & Query) => get<T.PagedResult<T.InvoiceListItem>>('/invoices', q),
  invoice: (id: number) => get<T.InvoiceDetailsResponse>(`/invoices/${id}`),
  payments: (q: T.PageQuery & Query) => get<T.PagedResult<T.PaymentListItem>>('/payments', q),
  initiatePayment: (invoiceId: number, method: string, idempotencyKey = newIdempotencyKey()) =>
    api.post<T.PaymentInitiated>('/payments', { invoiceId, method }, { headers: { 'Idempotency-Key': idempotencyKey } }).then((r) => r.data),
  confirmPayment: (paymentId: number, body: { gatewayOrderId: string; gatewayPaymentId: string; signature: string }) =>
    post<T.Payment>(`/payments/${paymentId}/confirm`, body),
  completeSandbox: (paymentId: number) => post<T.Payment>(`/payments/${paymentId}/sandbox/complete`),
  settlements: (q: T.PageQuery & Query) => get<T.PagedResult<T.SettlementListItem>>('/settlements', q),
  settlement: (id: number) => get<T.SettlementDetailsResponse>(`/settlements/${id}`),
  earnings: () => get<T.OwnerEarningsSummary>('/settlements/summary'),
};

export const customerApi = {
  me: () => get<T.Customer>('/customers/me'),
  update: (id: number, body: unknown) => put(`/customers/${id}`, body),
  addresses: (id: number) => get<T.CustomerAddress[]>(`/customers/${id}/addresses`),
  saveAddress: (id: number, addressId: number | null, body: unknown) =>
    addressId ? put(`/customers/${id}/addresses/${addressId}`, body) : post<T.CreatedResponse>(`/customers/${id}/addresses`, body),
  deleteAddress: (id: number, addressId: number) => del(`/customers/${id}/addresses/${addressId}`),
};

export const ownerApi = {
  me: () => get<T.OwnerProfile>('/owners/me'),
  update: (id: number, body: unknown) => put(`/owners/${id}`, body),
  saveBusiness: (id: number, body: unknown) => put(`/owners/${id}/business`, body),
  addAddress: (id: number, body: unknown) => post<T.CreatedResponse>(`/owners/${id}/addresses`, body),
  addBankAccount: (id: number, body: unknown) => post<T.CreatedResponse>(`/owners/${id}/bank-accounts`, body),
  deactivateBankAccount: (id: number, accountId: number) => post(`/owners/${id}/bank-accounts/${accountId}/deactivate`),
  vehicles: (q: T.PageQuery & Query) => get<T.PagedResult<T.VehicleListItem>>('/vehicles', q),
  vehicle: (id: number) => get<T.Vehicle>(`/vehicles/${id}`),
  createVehicle: (body: unknown) => post<T.CreatedResponse>('/vehicles', body),
  updateVehicle: (id: number, body: unknown) => put(`/vehicles/${id}`, body),
  setVehicleAvailability: (id: number, isAvailable: boolean, reason?: string) => put(`/vehicles/${id}/availability`, { isAvailable, reason }),
  drivers: (q: T.PageQuery & Query) => get<T.PagedResult<T.DriverListItem>>('/drivers', q),
  driver: (id: number) => get<T.Driver>(`/drivers/${id}`),
  createDriver: (body: unknown) => post<T.CreatedResponse>('/drivers', body),
  setDriverActive: (id: number, active: boolean) => post(`/drivers/${id}/${active ? 'activate' : 'deactivate'}`),
};

export const driverApi = {
  me: () => get<T.Driver>('/drivers/me'),
  setAvailability: (id: number, status: string, reason?: string) => put(`/drivers/${id}/availability`, { status, reason }),
};

export const documentApi = {
  list: (entity: T.DocumentEntityType, entityId: number) => get<T.DocumentItem[]>(`/documents/${entity}/${entityId}`),
  upload: (entity: T.DocumentEntityType, entityId: number, form: FormData) =>
    api.post<T.CreatedResponse>(`/documents/${entity}/${entityId}`, form, { headers: { 'Content-Type': 'multipart/form-data' } }).then((r) => r.data),
  remove: (entity: T.DocumentEntityType, entityId: number, documentId: number) => del(`/documents/${entity}/${entityId}/${documentId}`),
  fileUrl: (entity: T.DocumentEntityType, entityId: number, documentId: number) => `/documents/${entity}/${entityId}/${documentId}/file`,
};

export const supportApi = {
  tickets: (q: T.PageQuery & Query) => get<T.PagedResult<T.TicketListItem>>('/support/tickets', q),
  ticket: (id: number) => get<T.TicketDetailsResponse>(`/support/tickets/${id}`),
  createTicket: (body: unknown) => post<T.CreatedResponse>('/support/tickets', body),
  comment: (id: number, commentText: string) => post<T.CreatedResponse>(`/support/tickets/${id}/comments`, { commentText }),
  complaints: (q: T.PageQuery & Query) => get<T.PagedResult<T.ComplaintListItem>>('/support/complaints', q),
  complaint: (id: number) => get<{ complaint: T.Complaint }>(`/support/complaints/${id}`),
  createComplaint: (body: unknown) => post<T.CreatedResponse>('/support/complaints', body),
};

export const notificationApi = {
  list: (q: T.PageQuery & { unreadOnly?: boolean }) => get<T.PagedResult<T.NotificationItem>>('/notifications', q),
  unreadCount: () => get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: number) => post(`/notifications/${id}/read`),
  markAllRead: () => post('/notifications/read-all'),
};

export const dashboardApi = {
  customer: () => get<T.CustomerDashboard>('/dashboard/customer'),
  owner: () => get<T.OwnerDashboard>('/dashboard/owner'),
  driver: () => get<T.DriverDashboard>('/dashboard/driver'),
};
