import { api, newIdempotencyKey, PORTAL } from './client';
import type * as T from './types';

type Query = Record<string, string | number | boolean | null | undefined>;
type Paged = T.PageQuery & Query;

const clean = (q?: Query) =>
  q ? Object.fromEntries(Object.entries(q).filter(([, v]) => v !== undefined && v !== null && v !== '')) : undefined;

export const get = async <R>(url: string, params?: Query) => (await api.get<R>(url, { params: clean(params) })).data;
export const post = async <R = void>(url: string, body?: unknown) => (await api.post<R>(url, body ?? {})).data;
export const put = async <R = void>(url: string, body?: unknown) => (await api.put<R>(url, body ?? {})).data;
const del = async (url: string) => {
  await api.delete(url);
};

export const authApi = {
  login: (email: string, password: string) => post<T.AuthResponse>('/auth/login', { email, password, portal: PORTAL }),
  logout: () => post<T.MessageResponse>('/auth/logout', { portal: PORTAL }),
  forgotPassword: (email: string) => post<T.MessageResponse>('/auth/forgot-password', { email, portal: PORTAL }),
  resetPassword: (token: string, newPassword: string) => post<T.MessageResponse>('/auth/reset-password', { token, newPassword }),
  changePassword: (currentPassword: string, newPassword: string) =>
    post<T.MessageResponse>('/auth/change-password', { currentPassword, newPassword }),
};

export const referenceApi = {
  reference: () => get<T.ReferenceData>('/master-data/reference'),
  cities: (stateId?: number) => get<T.City[]>('/master-data/cities', { stateId }),
  documentTypes: (appliesTo: string) => get<T.DocumentType[]>('/master-data/document-types', { appliesTo }),
};

export const masterDataApi = {
  states: () => get<T.StateItem[]>('/master-data/states', { includeInactive: true }),
  saveState: (id: number | null, body: unknown) => (id ? put(`/master-data/states/${id}`, body) : post('/master-data/states', body)),
  cities: () => get<T.City[]>('/master-data/cities', { includeInactive: true }),
  saveCity: (id: number | null, body: unknown) => (id ? put(`/master-data/cities/${id}`, body) : post('/master-data/cities', body)),
  vehicleTypes: () => get<T.VehicleType[]>('/master-data/vehicle-types', { includeInactive: true }),
  saveVehicleType: (id: number | null, body: unknown) =>
    id ? put(`/master-data/vehicle-types/${id}`, body) : post('/master-data/vehicle-types', body),
  goodsTypes: () => get<T.GoodsType[]>('/master-data/goods-types', { includeInactive: true }),
  saveGoodsType: (id: number | null, body: unknown) => (id ? put(`/master-data/goods-types/${id}`, body) : post('/master-data/goods-types', body)),
  documentTypes: () => get<T.DocumentType[]>('/master-data/document-types', { includeInactive: true }),
  saveDocumentType: (id: number | null, body: unknown) =>
    id ? put(`/master-data/document-types/${id}`, body) : post('/master-data/document-types', body),
  settings: () => get<T.SystemSetting[]>('/settings'),
  saveSetting: (key: string, value: string) => put(`/settings/${encodeURIComponent(key)}`, { value }),
};

export const bookingApi = {
  list: (q: Paged) => get<T.PagedResult<T.BookingListItem>>('/bookings', q),
  get: (id: number) => get<T.BookingDetailsResponse>(`/bookings/${id}`),
  history: (id: number) => get<T.StatusHistory[]>(`/bookings/${id}/history`),
  notes: (id: number) => get<T.BookingNote[]>(`/bookings/${id}/notes`),
  addNote: (id: number, note: string, isInternal: boolean) => post<T.CreatedResponse>(`/bookings/${id}/notes`, { note, isInternal }),
  action: (id: number, action: 'review' | 'hold' | 'resume' | 'reject', remarks?: string) => post(`/bookings/${id}/${action}`, { remarks }),
  cancel: (id: number, reason: string) => post(`/bookings/${id}/cancel`, { reason }),
};

export const quotationApi = {
  list: (q: Paged) => get<T.PagedResult<T.QuotationListItem>>('/quotations', q),
  get: (id: number) => get<T.QuotationDetailsResponse>(`/quotations/${id}`),
  preview: (body: unknown) => post<T.PriceEstimate>('/quotations/preview', body),
  create: (body: unknown) => post<T.CreatedResponse>('/quotations', body),
  update: (id: number, body: unknown) => put(`/quotations/${id}`, body),
  send: (id: number) => post(`/quotations/${id}/send`),
  withdraw: (id: number, reason: string) => post(`/quotations/${id}/withdraw`, { reason }),
};

export const tripApi = {
  list: (q: Paged) => get<T.PagedResult<T.TripListItem>>('/trips', q),
  get: (id: number) => get<T.TripDetailsResponse>(`/trips/${id}`),
  create: (body: unknown) => post<T.CreatedResponse>('/trips', body),
  history: (id: number) => get<T.StatusHistory[]>(`/trips/${id}/history`),
  tracking: (id: number, sinceUtc?: string) => get<T.TripTracking>(`/trips/${id}/tracking`, { sinceUtc }),
  assignments: (id: number) => get<T.TripAssignment[]>(`/trips/${id}/assignments`),
  reassignVehicle: (id: number, vehicleId: number, reason: string) => post(`/trips/${id}/reassign-vehicle`, { vehicleId, reason }),
  reassignDriver: (id: number, driverId: number, reason: string) => post(`/trips/${id}/reassign-driver`, { driverId, reason }),
  hold: (id: number, reason: string) => post(`/trips/${id}/hold`, { reason }),
  resume: (id: number) => post(`/trips/${id}/resume`),
  resolveException: (id: number, reason: string) => post(`/trips/${id}/exception/resolve`, { reason }),
  cancel: (id: number, reason: string) => post(`/trips/${id}/cancel`, { reason }),
  pod: (id: number) => get<T.ProofOfDeliveryResponse>(`/trips/${id}/proof-of-delivery`),
  podFileUrl: (id: number, fileId: number) => `/trips/${id}/proof-of-delivery/files/${fileId}`,
  availableVehicles: (vehicleTypeId: number, minCapacityKg: number, onDateUtc: string) =>
    get<T.AvailableVehicle[]>('/vehicles/available', { vehicleTypeId, minCapacityKg, onDateUtc }),
  availableDrivers: (ownerId: number | null, onDateUtc: string) => get<T.AvailableDriver[]>('/drivers/available', { ownerId, onDateUtc }),
};

export const partnerApi = {
  customers: (q: Paged) => get<T.PagedResult<T.CustomerListItem>>('/customers', q),
  customer: (id: number) => get<T.Customer>(`/customers/${id}`),
  customerAddresses: (id: number) => get<T.CustomerAddress[]>(`/customers/${id}/addresses`),
  setCustomerActive: (id: number, active: boolean) => post(`/customers/${id}/${active ? 'activate' : 'deactivate'}`),
  owners: (q: Paged) => get<T.PagedResult<T.OwnerListItem>>('/owners', q),
  owner: (id: number) => get<T.OwnerProfile>(`/owners/${id}`),
  verifyOwner: (id: number, status: string, remarks?: string) => post(`/owners/${id}/verify`, { status, remarks }),
  setOwnerActive: (id: number, active: boolean) => post(`/owners/${id}/${active ? 'activate' : 'deactivate'}`),
  verifyBankAccount: (id: number, accountId: number, status: string, remarks?: string) =>
    post(`/owners/${id}/bank-accounts/${accountId}/verify`, { status, remarks }),
  revealBankAccount: (id: number, accountId: number) => post<T.BankAccountReveal>(`/owners/${id}/bank-accounts/${accountId}/reveal`),
  drivers: (q: Paged) => get<T.PagedResult<T.DriverListItem>>('/drivers', q),
  driver: (id: number) => get<T.Driver>(`/drivers/${id}`),
  verifyDriver: (id: number, status: string, remarks?: string) => post(`/drivers/${id}/verify`, { status, remarks }),
  setDriverActive: (id: number, active: boolean) => post(`/drivers/${id}/${active ? 'activate' : 'deactivate'}`),
  vehicles: (q: Paged) => get<T.PagedResult<T.VehicleListItem>>('/vehicles', q),
  vehicle: (id: number) => get<T.Vehicle>(`/vehicles/${id}`),
  verifyVehicle: (id: number, status: string, remarks?: string) => post(`/vehicles/${id}/verify`, { status, remarks }),
  setVehicleActive: (id: number, active: boolean) => post(`/vehicles/${id}/${active ? 'activate' : 'deactivate'}`),
  expiringVehicles: (withinDays: number) => get<T.ExpiringVehicle[]>('/vehicles/expiring', { withinDays }),
};

export const documentApi = {
  list: (entity: T.DocumentEntityType, entityId: number) => get<T.DocumentItem[]>(`/documents/${entity}/${entityId}`),
  verify: (entity: T.DocumentEntityType, entityId: number, documentId: number, status: string, remarks?: string) =>
    post(`/documents/${entity}/${entityId}/${documentId}/verify`, { status, remarks }),
  upload: (entity: T.DocumentEntityType, entityId: number, form: FormData) =>
    api.post<T.CreatedResponse>(`/documents/${entity}/${entityId}`, form, { headers: { 'Content-Type': 'multipart/form-data' } }).then((r) => r.data),
  remove: (entity: T.DocumentEntityType, entityId: number, documentId: number) => del(`/documents/${entity}/${entityId}/${documentId}`),
  fileUrl: (entity: T.DocumentEntityType, entityId: number, documentId: number) => `/documents/${entity}/${entityId}/${documentId}/file`,
};

export const financeApi = {
  invoices: (q: Paged) => get<T.PagedResult<T.InvoiceListItem>>('/invoices', q),
  invoice: (id: number) => get<T.InvoiceDetailsResponse>(`/invoices/${id}`),
  createInvoice: (bookingId: number, paymentTermsDays?: number) => post<T.CreatedResponse>('/invoices', { bookingId, paymentTermsDays }),
  adjustInvoice: (id: number, body: unknown) => post(`/invoices/${id}/adjustments`, body),
  cancelInvoice: (id: number, reason: string) => post(`/invoices/${id}/cancel`, { reason }),
  payments: (q: Paged) => get<T.PagedResult<T.PaymentListItem>>('/payments', q),
  payment: (id: number) => get<T.PaymentDetails>(`/payments/${id}`),
  recordOffline: (body: unknown) =>
    api.post<T.CreatedResponse>('/payments/offline', body, { headers: { 'Idempotency-Key': newIdempotencyKey() } }).then((r) => r.data),
  refund: (id: number, amount: number, reason: string) => post<T.CreatedResponse>(`/payments/${id}/refunds`, { amount, reason }),
  reconciliation: (fromUtc: string, toUtc: string) => get<T.ReconciliationRow[]>('/payments/reconciliation', { fromUtc, toUtc }),
  settlements: (q: Paged) => get<T.PagedResult<T.SettlementListItem>>('/settlements', q),
  settlement: (id: number) => get<T.SettlementDetailsResponse>(`/settlements/${id}`),
  eligibleTrips: (q: Paged) => get<T.PagedResult<T.EligibleTrip>>('/settlements/eligible-trips', q),
  createSettlement: (tripId: number) => post<T.CreatedResponse>('/settlements', { tripId }),
  adjustSettlement: (id: number, description: string, amount: number) => post(`/settlements/${id}/adjustments`, { description, amount }),
  settlementAction: (id: number, action: 'approve' | 'process') => post(`/settlements/${id}/${action}`),
  completeSettlement: (id: number, transactionReference: string) => post(`/settlements/${id}/complete`, { transactionReference }),
  failSettlement: (id: number, reason: string) => post(`/settlements/${id}/fail`, { reason }),
  cancelSettlement: (id: number, reason: string) => post(`/settlements/${id}/cancel`, { reason }),
};

export const pricingApi = {
  configuration: () => get<T.PricingConfiguration>('/pricing/configuration'),
  save: (kind: 'vehicle-rates' | 'distance-slabs' | 'additional-charges' | 'rules' | 'tax-rates' | 'commission-rules', id: number | null, body: unknown) =>
    id ? put(`/pricing/${kind}/${id}`, body) : post(`/pricing/${kind}`, body),
};

export const adminApi = {
  users: (q: Paged) => get<T.PagedResult<T.UserListItem>>('/users', q),
  user: (id: number) => get<T.UserDetails>(`/users/${id}`),
  createUser: (body: unknown) => post<T.CreatedResponse>('/users', body),
  updateUser: (id: number, body: unknown) => put(`/users/${id}`, body),
  setUserRoles: (id: number, roleIds: number[]) => put(`/users/${id}/roles`, { roleIds }),
  userAction: (id: number, action: 'activate' | 'deactivate' | 'unlock') => post(`/users/${id}/${action}`),
  roles: () => get<T.Role[]>('/roles'),
  role: (id: number) => get<T.RoleDetails>(`/roles/${id}`),
  permissions: () => get<T.Permission[]>('/roles/permissions'),
  createRole: (body: unknown) => post<T.CreatedResponse>('/roles', body),
  updateRole: (id: number, body: unknown) => put(`/roles/${id}`, body),
  setRolePermissions: (id: number, permissionIds: number[]) => put(`/roles/${id}/permissions`, { permissionIds }),
  deleteRole: (id: number) => del(`/roles/${id}`),
  auditLogs: (q: Paged) => get<T.PagedResult<T.AuditLog>>('/audit-logs', q),
  templates: () => get<T.NotificationTemplate[]>('/notifications/templates'),
  saveTemplate: (id: number, body: unknown) => put(`/notifications/templates/${id}`, body),
};

export const supportApi = {
  tickets: (q: Paged) => get<T.PagedResult<T.TicketListItem>>('/support/tickets', q),
  ticket: (id: number) => get<T.TicketDetailsResponse>(`/support/tickets/${id}`),
  updateTicket: (id: number, body: unknown) => put(`/support/tickets/${id}`, body),
  comment: (id: number, commentText: string, isInternal: boolean) => post(`/support/tickets/${id}/comments`, { commentText, isInternal }),
  complaints: (q: Paged) => get<T.PagedResult<T.ComplaintListItem>>('/support/complaints', q),
  complaint: (id: number) => get<T.ComplaintDetails>(`/support/complaints/${id}`),
  assignComplaint: (id: number, assignedToUserId: number) => post(`/support/complaints/${id}/assign`, { assignedToUserId }),
  complaintStatus: (id: number, status: string, resolution?: string) => post(`/support/complaints/${id}/status`, { status, resolution }),
  enquiries: (q: Paged) => get<T.PagedResult<T.ContactEnquiry>>('/support/enquiries', q),
  enquiryHandled: (id: number) => post(`/support/enquiries/${id}/handled`),
};

export const notificationApi = {
  list: (q: Paged) => get<T.PagedResult<T.NotificationItem>>('/notifications', q),
  unreadCount: () => get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: number) => post(`/notifications/${id}/read`),
  markAllRead: () => post('/notifications/read-all'),
};

export const reportApi = {
  dashboard: () => get<T.OperationsDashboard>('/dashboard/operations'),
  bookingsByDay: (from: string, to: string) => get<T.BookingsByDay[]>('/reports/bookings-by-day', { from, to }),
  bookingsByStatus: (from: string, to: string) => get<T.StatusCount[]>('/reports/bookings-by-status', { from, to }),
  revenue: (from: string, to: string, groupBy: string) => get<T.RevenueRow[]>('/reports/revenue', { from, to, groupBy }),
  tripPerformance: (from: string, to: string) => get<T.TripPerformanceRow[]>('/reports/trip-performance', { from, to }),
  settlements: (from: string, to: string) => get<T.SettlementReportRow[]>('/reports/settlements', { from, to }),
  topCustomers: (from: string, to: string) => get<T.TopCustomer[]>('/reports/top-customers', { from, to, top: 10 }),
  partnerSummary: () => get<T.PartnerSummaryRow[]>('/reports/partner-summary'),
  exportUrl: (report: string, from: string, to: string, groupBy = 'Day') =>
    `/reports/${report}/export?from=${from}&to=${to}&groupBy=${groupBy}`,
};
