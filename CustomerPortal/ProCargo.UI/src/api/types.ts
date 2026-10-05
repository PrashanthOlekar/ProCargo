/** API contracts (camelCase JSON of the ProCargo.API DTOs). Dates are ISO strings in UTC; DateOnly is "YYYY-MM-DD". */

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
}

export interface PageQuery {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreatedResponse {
  id: number;
  number: string | null;
}

export interface MessageResponse {
  success: boolean;
  message: string;
}

export interface StatusHistory {
  historyId: number;
  fromStatusId: number | null;
  toStatusId: number;
  remarks: string | null;
  changedBy: number | null;
  changedByName: string | null;
  changedDateUtc: string;
}

// ---------- identity ----------

export interface CurrentUser {
  userId: number;
  email: string;
  fullName: string;
  phoneNumber: string;
  portal: string;
  roles: string[];
  permissions: string[];
  customerId: number | null;
  ownerId: number | null;
  driverId: number | null;
  mustChangePassword: boolean;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  user: CurrentUser;
}

export type AccountType = 'Customer' | 'VehicleOwner' | 'Driver';

// ---------- reference data ----------

export interface VehicleType {
  vehicleTypeId: number;
  code: string;
  name: string;
  description: string | null;
  capacityKg: number;
  lengthFt: number | null;
  sortOrder: number;
  isActive: boolean;
}

export interface GoodsType {
  goodsTypeId: number;
  code: string;
  name: string;
  requiresSpecialHandling: boolean;
  isActive: boolean;
}

export interface StateItem {
  stateId: number;
  stateCode: string;
  name: string;
  isActive: boolean;
}

export interface City {
  cityId: number;
  stateId: number;
  stateName: string;
  name: string;
  isActive: boolean;
}

export interface DocumentType {
  documentTypeId: number;
  code: string;
  name: string;
  appliesTo: string;
  requiresExpiry: boolean;
  isMandatory: boolean;
  isActive: boolean;
}

export interface ReferenceData {
  vehicleTypes: VehicleType[];
  goodsTypes: GoodsType[];
  states: StateItem[];
}

// ---------- pricing ----------

export interface PriceLine {
  chargeCode: string;
  description: string;
  quantity: number;
  unitRate: number;
  amount: number;
}

export interface PriceEstimate {
  distanceKm: number;
  subTotal: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
  lines: PriceLine[];
}

// ---------- bookings ----------

export interface BookingListItem {
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  vehicleTypeId: number;
  vehicleTypeName: string;
  totalWeightKg: number;
  pickupCityName: string;
  deliveryCityName: string;
  requestedPickupDateUtc: string;
  bookingStatusId: number;
  createdDateUtc: string;
}

export interface BookingDetails {
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  customerCompanyName: string | null;
  customerPhoneNumber: string;
  vehicleTypeId: number;
  vehicleTypeName: string;
  goodsTypeId: number;
  goodsTypeName: string;
  requiresSpecialHandling: boolean;
  goodsDescription: string;
  totalWeightKg: number;
  totalQuantity: number;
  requestedPickupDateUtc: string;
  specialInstructions: string | null;
  estimatedDistanceKm: number | null;
  bookingStatusId: number;
  cancellationReason: string | null;
  pickupAddressLine1: string;
  pickupAddressLine2: string | null;
  pickupLandmark: string | null;
  pickupCityId: number;
  pickupCityName: string;
  pickupStateId: number;
  pickupPincode: string;
  pickupLatitude: number | null;
  pickupLongitude: number | null;
  deliveryAddressLine1: string;
  deliveryAddressLine2: string | null;
  deliveryLandmark: string | null;
  deliveryCityId: number;
  deliveryCityName: string;
  deliveryStateId: number;
  deliveryPincode: string;
  deliveryLatitude: number | null;
  deliveryLongitude: number | null;
  pickupContactName: string;
  pickupContactPhone: string;
  pickupContactAltPhone: string | null;
  deliveryContactName: string;
  deliveryContactPhone: string;
  deliveryContactAltPhone: string | null;
  acceptedQuotationId: number | null;
  acceptedQuotationAmount: number | null;
  tripId: number | null;
  tripNumber: string | null;
  tripStatusId: number | null;
  assignedVehicleNumber: string | null;
  assignedDriverName: string | null;
  assignedDriverPhone: string | null;
  invoiceId: number | null;
  createdDateUtc: string;
  rowVersion: string;
}

export interface BookingItem {
  bookingItemId: number;
  description: string;
  quantity: number;
  weightKg: number;
  lengthCm: number | null;
  widthCm: number | null;
  heightCm: number | null;
  isFragile: boolean;
  declaredValue: number | null;
}

export interface BookingDetailsResponse {
  booking: BookingDetails;
  items: BookingItem[];
  availableActions: string[];
}

export interface AddressInput {
  addressLine1: string;
  addressLine2?: string | null;
  landmark?: string | null;
  cityId: number;
  pincode: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface ContactInput {
  contactName: string;
  phoneNumber: string;
  alternatePhoneNumber?: string | null;
}

export interface BookingItemInput {
  description: string;
  quantity: number;
  weightKg: number;
  lengthCm?: number | null;
  widthCm?: number | null;
  heightCm?: number | null;
  isFragile: boolean;
  declaredValue?: number | null;
}

export interface BookingInput {
  vehicleTypeId: number;
  goodsTypeId: number;
  goodsDescription: string;
  requestedPickupDateUtc: string;
  specialInstructions?: string | null;
  estimatedDistanceKm?: number | null;
  pickupAddress: AddressInput;
  deliveryAddress: AddressInput;
  pickupContact: ContactInput;
  deliveryContact: ContactInput;
  items: BookingItemInput[];
}

// ---------- quotations ----------

export interface QuotationListItem {
  quotationId: number;
  quotationNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  versionNo: number;
  totalAmount: number;
  validityDateUtc: string;
  quotationStatusId: number;
  createdDateUtc: string;
}

export interface Quotation {
  quotationId: number;
  quotationNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  bookingStatusId: number;
  versionNo: number;
  distanceKm: number;
  baseAmount: number;
  distanceCharge: number;
  loadingCharge: number;
  unloadingCharge: number;
  waitingCharge: number;
  tollCharge: number;
  nightCharge: number;
  specialHandlingCharge: number;
  adjustmentAmount: number;
  discountAmount: number;
  subTotal: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
  validityDateUtc: string;
  quotationStatusId: number;
  notes: string | null;
  rejectionReason: string | null;
  sentDateUtc: string | null;
  respondedDateUtc: string | null;
  createdDateUtc: string;
  rowVersion: string;
}

export interface QuotationCharge {
  quotationChargeId: number;
  chargeCode: string;
  description: string;
  quantity: number;
  unitRate: number;
  amount: number;
  sortOrder: number;
}

export interface QuotationDetailsResponse {
  quotation: Quotation;
  charges: QuotationCharge[];
  availableActions: string[];
}

// ---------- trips ----------

export interface TripListItem {
  tripId: number;
  tripNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerName: string;
  vehicleNumber: string;
  driverName: string;
  ownerName: string;
  pickupCityName: string;
  deliveryCityName: string;
  tripStatusId: number;
  plannedPickupDateUtc: string;
  plannedDeliveryDateUtc: string;
  createdDateUtc: string;
}

export interface Trip {
  tripId: number;
  tripNumber: string;
  bookingId: number;
  bookingNumber: string;
  bookingStatusId: number;
  customerId: number;
  customerName: string;
  quotationId: number;
  vehicleId: number;
  vehicleNumber: string;
  vehicleTypeId: number;
  vehicleTypeName: string;
  driverId: number;
  driverName: string;
  driverPhoneNumber: string;
  ownerId: number;
  ownerName: string;
  tripStatusId: number;
  statusBeforeHoldId: number | null;
  plannedPickupDateUtc: string;
  actualPickupDateUtc: string | null;
  plannedDeliveryDateUtc: string;
  actualDeliveryDateUtc: string | null;
  startOdometer: number | null;
  endOdometer: number | null;
  exceptionReason: string | null;
  cancellationReason: string | null;
  pickupAddress: string;
  pickupCityName: string;
  pickupLatitude: number | null;
  pickupLongitude: number | null;
  deliveryAddress: string;
  deliveryCityName: string;
  deliveryLatitude: number | null;
  deliveryLongitude: number | null;
  pickupContactName: string;
  pickupContactPhone: string;
  deliveryContactName: string;
  deliveryContactPhone: string;
  goodsDescription: string;
  totalWeightKg: number;
  totalQuantity: number;
  specialInstructions: string | null;
  hasProofOfDelivery: boolean;
  createdDateUtc: string;
}

export interface TripDetailsResponse {
  trip: Trip;
  availableActions: string[];
}

export interface TripLocation {
  tripLocationHistoryId: number;
  latitude: number;
  longitude: number;
  recordedDateUtc: string;
  speedKmph: number | null;
  heading: number | null;
  accuracy: number | null;
  trackingProviderId: number;
}

export interface TripTracking {
  tripId: number;
  tripStatusId: number;
  lastLocation: TripLocation | null;
  points: TripLocation[];
}

export interface OtpSent {
  sentTo: string;
  expiresDateUtc: string;
  testOtp?: string;
}

export interface ProofOfDelivery {
  proofOfDeliveryId: number;
  tripId: number;
  receiverName: string;
  receiverPhone: string | null;
  deliveredDateUtc: string;
  remarks: string | null;
  latitude: number | null;
  longitude: number | null;
  signatureFileId: number | null;
  uploadedByName: string | null;
  createdDateUtc: string;
}

export interface ProofOfDeliveryFile {
  proofOfDeliveryFileId: number;
  storedFileId: number;
  fileCategory: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  createdDateUtc: string;
}

export interface ProofOfDeliveryResponse {
  proofOfDelivery: ProofOfDelivery;
  files: ProofOfDeliveryFile[];
}

// ---------- finance ----------

export interface InvoiceListItem {
  invoiceId: number;
  invoiceNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  invoiceDateUtc: string;
  dueDateUtc: string;
  totalAmount: number;
  paidAmount: number;
  invoiceStatusId: number;
  isOverdue: boolean;
}

export interface Invoice {
  invoiceId: number;
  invoiceNumber: string;
  bookingId: number;
  bookingNumber: string;
  tripId: number;
  tripNumber: string;
  customerId: number;
  customerName: string;
  customerCompanyName: string | null;
  customerGstNumber: string | null;
  invoiceDateUtc: string;
  dueDateUtc: string;
  subTotal: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  invoiceStatusId: number;
  cancellationReason: string | null;
  createdDateUtc: string;
  rowVersion: string;
}

export interface InvoiceItem {
  invoiceItemId: number;
  chargeCode: string;
  description: string;
  quantity: number;
  unitRate: number;
  amount: number;
  isAdjustment: boolean;
  sortOrder: number;
}

export interface PaymentListItem {
  paymentId: number;
  paymentNumber: string;
  invoiceId: number;
  invoiceNumber: string;
  customerId: number;
  customerName: string;
  amount: number;
  refundedAmount: number;
  paymentMethodId: number;
  paymentStatusId: number;
  gatewayName: string;
  paymentDateUtc: string | null;
  createdDateUtc: string;
}

export interface InvoiceDetailsResponse {
  invoice: Invoice;
  items: InvoiceItem[];
  payments: PaymentListItem[];
  availableActions: string[];
}

export interface Payment {
  paymentId: number;
  paymentNumber: string;
  invoiceId: number;
  invoiceNumber: string;
  amount: number;
  paymentStatusId: number;
  paymentMethodId: number;
  gatewayName: string;
  paymentDateUtc: string | null;
  failureReason: string | null;
}

export interface PaymentInitiated {
  paymentId: number;
  paymentNumber: string;
  isExisting: boolean;
  checkout: {
    gateway: string;
    orderId: string;
    publicKey: string;
    amount: number;
    currency: string;
    checkoutUrl: string | null;
  };
}

export interface SettlementListItem {
  settlementId: number;
  settlementNumber: string;
  tripId: number;
  tripNumber: string;
  ownerId: number;
  ownerName: string;
  grossAmount: number;
  commissionAmount: number;
  netAmount: number;
  settlementStatusId: number;
  settlementDateUtc: string | null;
  createdDateUtc: string;
}

export interface Settlement {
  settlementId: number;
  settlementNumber: string;
  tripId: number;
  tripNumber: string;
  bookingNumber: string;
  ownerId: number;
  ownerName: string;
  bankName: string | null;
  accountNumberLast4: string | null;
  grossAmount: number;
  commissionPercent: number;
  commissionAmount: number;
  taxAmount: number;
  adjustmentAmount: number;
  netAmount: number;
  settlementStatusId: number;
  approvedDateUtc: string | null;
  settlementDateUtc: string | null;
  transactionReference: string | null;
  failureReason: string | null;
  createdDateUtc: string;
}

export interface SettlementItem {
  settlementItemId: number;
  itemType: string;
  description: string;
  amount: number;
  sortOrder: number;
}

export interface SettlementDetailsResponse {
  settlement: Settlement;
  items: SettlementItem[];
  availableActions: string[];
}

export interface OwnerEarningsSummary {
  totalEarned: number;
  pendingAmount: number;
  completedCount: number;
  pendingCount: number;
  lastSettlementDateUtc: string | null;
}

// ---------- partners ----------

export interface Customer {
  customerId: number;
  customerNumber: string;
  customerTypeId: number;
  fullName: string;
  companyName: string | null;
  email: string;
  phoneNumber: string;
  gstNumber: string | null;
  isActive: boolean;
  createdDateUtc: string;
  rowVersion: string;
}

export interface CustomerAddress {
  customerAddressId: number;
  customerId: number;
  label: string;
  addressLine1: string;
  addressLine2: string | null;
  landmark: string | null;
  cityId: number;
  cityName: string;
  stateId: number;
  stateName: string;
  pincode: string;
  latitude: number | null;
  longitude: number | null;
  isDefault: boolean;
}

export interface Owner {
  ownerId: number;
  ownerNumber: string;
  ownerTypeId: number;
  fullName: string;
  email: string;
  phoneNumber: string;
  panLast4: string | null;
  verificationStatusId: number;
  verificationRemarks: string | null;
  isActive: boolean;
  businessName: string | null;
  gstNumber: string | null;
  registrationNumber: string | null;
  fleetSize: number | null;
  createdDateUtc: string;
  rowVersion: string;
}

export interface OwnerAddress {
  ownerAddressId: number;
  addressLine1: string;
  addressLine2: string | null;
  landmark: string | null;
  cityId: number;
  cityName: string;
  stateName: string;
  pincode: string;
  isPrimary: boolean;
}

export interface OwnerBankAccount {
  ownerBankAccountId: number;
  accountHolderName: string;
  bankName: string;
  accountNumberLast4: string;
  ifscCode: string;
  isPrimary: boolean;
  verificationStatusId: number;
  isActive: boolean;
}

export interface OwnerProfile {
  owner: Owner;
  addresses: OwnerAddress[];
  bankAccounts: OwnerBankAccount[];
}

export interface DriverListItem {
  driverId: number;
  driverNumber: string;
  ownerId: number | null;
  ownerName: string | null;
  fullName: string;
  phoneNumber: string;
  licenseNumber: string | null;
  licenseExpiryDate: string | null;
  verificationStatusId: number;
  availabilityStatusId: number;
  isActive: boolean;
  createdDateUtc: string;
}

export interface Driver {
  driverId: number;
  driverNumber: string;
  ownerId: number | null;
  ownerName: string | null;
  fullName: string;
  email: string;
  phoneNumber: string;
  alternatePhoneNumber: string | null;
  dateOfBirth: string | null;
  licenseNumber: string | null;
  licenseClass: string | null;
  licenseIssueDate: string | null;
  licenseExpiryDate: string | null;
  licenseIssuingAuthority: string | null;
  verificationStatusId: number;
  verificationRemarks: string | null;
  availabilityStatusId: number;
  isActive: boolean;
  createdDateUtc: string;
  rowVersion: string;
}

export interface VehicleListItem {
  vehicleId: number;
  vehicleNumber: string;
  ownerId: number;
  ownerName: string;
  vehicleTypeId: number;
  vehicleTypeName: string;
  manufacturer: string;
  model: string;
  capacityKg: number;
  verificationStatusId: number;
  isAvailable: boolean;
  isActive: boolean;
  insuranceExpiryDate: string | null;
  permitExpiryDate: string | null;
  fitnessExpiryDate: string | null;
  createdDateUtc: string;
}

export interface Vehicle {
  vehicleId: number;
  ownerId: number;
  ownerName: string;
  vehicleNumber: string;
  vehicleTypeId: number;
  vehicleTypeName: string;
  manufacturer: string;
  model: string;
  manufactureYear: number | null;
  capacityKg: number;
  permitNumber: string | null;
  permitExpiryDate: string | null;
  insuranceNumber: string | null;
  insuranceExpiryDate: string | null;
  fitnessExpiryDate: string | null;
  pucExpiryDate: string | null;
  verificationStatusId: number;
  verificationRemarks: string | null;
  isAvailable: boolean;
  isActive: boolean;
  createdDateUtc: string;
  rowVersion: string;
}

export type DocumentEntityType = 'Customer' | 'Owner' | 'Driver' | 'Vehicle';

export interface DocumentItem {
  documentId: number;
  entityId: number;
  documentTypeId: number;
  documentTypeCode: string;
  documentTypeName: string;
  storedFileId: number;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  documentNumber: string | null;
  expiryDate: string | null;
  verificationStatusId: number;
  remarks: string | null;
  createdDateUtc: string;
}

// ---------- support / notifications / dashboards ----------

export interface TicketListItem {
  supportTicketId: number;
  ticketNumber: string;
  raisedByName: string;
  subject: string;
  ticketPriorityId: number;
  ticketStatusId: number;
  assignedToName: string | null;
  createdDateUtc: string;
}

export interface Ticket {
  supportTicketId: number;
  ticketNumber: string;
  raisedByUserId: number;
  raisedByName: string;
  bookingId: number | null;
  bookingNumber: string | null;
  subject: string;
  description: string;
  ticketPriorityId: number;
  ticketStatusId: number;
  assignedToName: string | null;
  closedDateUtc: string | null;
  createdDateUtc: string;
}

export interface TicketComment {
  supportTicketCommentId: number;
  commentText: string;
  isInternal: boolean;
  createdByName: string;
  isStaff: boolean;
  createdDateUtc: string;
}

export interface TicketDetailsResponse {
  ticket: Ticket;
  comments: TicketComment[];
}

export interface ComplaintListItem {
  complaintId: number;
  complaintNumber: string;
  raisedByName: string;
  category: string;
  subject: string;
  complaintStatusId: number;
  assignedToName: string | null;
  createdDateUtc: string;
}

export interface Complaint {
  complaintId: number;
  complaintNumber: string;
  bookingNumber: string | null;
  tripNumber: string | null;
  category: string;
  subject: string;
  description: string;
  complaintStatusId: number;
  resolution: string | null;
  resolvedDateUtc: string | null;
  createdDateUtc: string;
}

export interface NotificationItem {
  notificationId: number;
  templateCode: string;
  title: string;
  message: string;
  entityType: string | null;
  entityId: number | null;
  readDateUtc: string | null;
  createdDateUtc: string;
}

export interface CustomerDashboard {
  activeBookings: number;
  awaitingQuotationResponse: number;
  inTransit: number;
  completedBookings: number;
  outstandingAmount: number;
  totalPaid: number;
}

export interface OwnerDashboard {
  totalVehicles: number;
  availableVehicles: number;
  vehiclesPendingVerification: number;
  totalDrivers: number;
  activeTrips: number;
  completedTrips: number;
  pendingSettlementAmount: number;
  totalEarned: number;
}

export interface DriverDashboard {
  activeTrips: number;
  upcomingTrips: number;
  completedTrips: number;
  availabilityStatusId: number;
}
