/*
    Indexes are driven by the queries in 08-StoredProcedures. Primary keys and UNIQUE constraints
    (business numbers, emails, vehicle numbers, token hashes) already create their own indexes.

    Guidelines followed
      * Every FK used for "list children of parent" lookups gets an index (SQL Server does not create them).
      * Paged list procs filter on status + order by CreatedDateUtc DESC, so composite indexes
        (FilterColumn, CreatedDateUtc DESC) serve both the WHERE and the ORDER BY without a sort.
      * Filtered indexes enforce "one active X per Y" rules and keep hot-path indexes small.
      * INCLUDE columns only where a proc is known to be hot (dashboard / login / tracking).
*/

-- ===== sec =====
-- Login lookup by normalised email; filtered so a deleted account's email can be reused.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_User_NormalizedEmail' AND object_id = OBJECT_ID(N'sec.[User]'))
    CREATE UNIQUE INDEX UX_User_NormalizedEmail ON sec.[User] (NormalizedEmail) INCLUDE (PasswordHash, IsActive, LockoutEndUtc, FailedLoginCount) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_User_PhoneNumber' AND object_id = OBJECT_ID(N'sec.[User]'))
    CREATE UNIQUE INDEX UX_User_PhoneNumber ON sec.[User] (PhoneNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_User_IsInternal_CreatedDateUtc' AND object_id = OBJECT_ID(N'sec.[User]'))
    CREATE INDEX IX_User_IsInternal_CreatedDateUtc ON sec.[User] (IsInternal, CreatedDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserRole_RoleId' AND object_id = OBJECT_ID(N'sec.UserRole'))
    CREATE INDEX IX_UserRole_RoleId ON sec.UserRole (RoleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RolePermission_PermissionId' AND object_id = OBJECT_ID(N'sec.RolePermission'))
    CREATE INDEX IX_RolePermission_PermissionId ON sec.RolePermission (PermissionId);
-- Revoke-all-for-user and family revocation on token reuse.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_UserId' AND object_id = OBJECT_ID(N'sec.RefreshToken'))
    CREATE INDEX IX_RefreshToken_UserId ON sec.RefreshToken (UserId) WHERE RevokedDateUtc IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefreshToken_FamilyId' AND object_id = OBJECT_ID(N'sec.RefreshToken'))
    CREATE INDEX IX_RefreshToken_FamilyId ON sec.RefreshToken (FamilyId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PasswordResetToken_UserId' AND object_id = OBJECT_ID(N'sec.PasswordResetToken'))
    CREATE INDEX IX_PasswordResetToken_UserId ON sec.PasswordResetToken (UserId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LoginHistory_UserId_CreatedDateUtc' AND object_id = OBJECT_ID(N'sec.LoginHistory'))
    CREATE INDEX IX_LoginHistory_UserId_CreatedDateUtc ON sec.LoginHistory (UserId, CreatedDateUtc DESC);
GO

-- ===== mst =====
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_City_StateId' AND object_id = OBJECT_ID(N'mst.City'))
    CREATE INDEX IX_City_StateId ON mst.City (StateId) INCLUDE (Name, IsActive);
GO

-- ===== core: customers / owners / drivers / vehicles =====
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_PhoneNumber' AND object_id = OBJECT_ID(N'core.Customer'))
    CREATE INDEX IX_Customer_PhoneNumber ON core.Customer (PhoneNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_CreatedDateUtc' AND object_id = OBJECT_ID(N'core.Customer'))
    CREATE INDEX IX_Customer_CreatedDateUtc ON core.Customer (CreatedDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerAddress_CustomerId' AND object_id = OBJECT_ID(N'core.CustomerAddress'))
    CREATE INDEX IX_CustomerAddress_CustomerId ON core.CustomerAddress (CustomerId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerContact_CustomerId' AND object_id = OBJECT_ID(N'core.CustomerContact'))
    CREATE INDEX IX_CustomerContact_CustomerId ON core.CustomerContact (CustomerId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerDocument_CustomerId' AND object_id = OBJECT_ID(N'core.CustomerDocument'))
    CREATE INDEX IX_CustomerDocument_CustomerId ON core.CustomerDocument (CustomerId) WHERE IsDeleted = 0;

-- Approval queues: "owners/drivers/vehicles pending verification" ordered by age.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleOwner_Verification_Created' AND object_id = OBJECT_ID(N'core.VehicleOwner'))
    CREATE INDEX IX_VehicleOwner_Verification_Created ON core.VehicleOwner (VerificationStatusId, CreatedDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleOwner_PhoneNumber' AND object_id = OBJECT_ID(N'core.VehicleOwner'))
    CREATE INDEX IX_VehicleOwner_PhoneNumber ON core.VehicleOwner (PhoneNumber) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_OwnerBusiness_GstNumber' AND object_id = OBJECT_ID(N'core.OwnerBusiness'))
    CREATE UNIQUE INDEX UX_OwnerBusiness_GstNumber ON core.OwnerBusiness (GstNumber) WHERE GstNumber IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OwnerAddress_OwnerId' AND object_id = OBJECT_ID(N'core.OwnerAddress'))
    CREATE INDEX IX_OwnerAddress_OwnerId ON core.OwnerAddress (OwnerId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OwnerBankAccount_OwnerId' AND object_id = OBJECT_ID(N'core.OwnerBankAccount'))
    CREATE INDEX IX_OwnerBankAccount_OwnerId ON core.OwnerBankAccount (OwnerId) WHERE IsActive = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OwnerDocument_OwnerId' AND object_id = OBJECT_ID(N'core.OwnerDocument'))
    CREATE INDEX IX_OwnerDocument_OwnerId ON core.OwnerDocument (OwnerId) WHERE IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Driver_OwnerId' AND object_id = OBJECT_ID(N'core.Driver'))
    CREATE INDEX IX_Driver_OwnerId ON core.Driver (OwnerId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Driver_Verification_Created' AND object_id = OBJECT_ID(N'core.Driver'))
    CREATE INDEX IX_Driver_Verification_Created ON core.Driver (VerificationStatusId, CreatedDateUtc DESC) WHERE IsDeleted = 0;
-- Assignment screen: verified + available drivers.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Driver_Availability' AND object_id = OBJECT_ID(N'core.Driver'))
    CREATE INDEX IX_Driver_Availability ON core.Driver (AvailabilityStatusId, VerificationStatusId) INCLUDE (OwnerId, FullName) WHERE IsDeleted = 0 AND IsActive = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Driver_PhoneNumber' AND object_id = OBJECT_ID(N'core.Driver'))
    CREATE INDEX IX_Driver_PhoneNumber ON core.Driver (PhoneNumber) WHERE IsDeleted = 0;
-- Only one current licence per driver and a licence number cannot be current for two drivers.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DriverLicense_Driver_Current' AND object_id = OBJECT_ID(N'core.DriverLicense'))
    CREATE UNIQUE INDEX UX_DriverLicense_Driver_Current ON core.DriverLicense (DriverId) WHERE IsCurrent = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_DriverLicense_Number_Current' AND object_id = OBJECT_ID(N'core.DriverLicense'))
    CREATE UNIQUE INDEX UX_DriverLicense_Number_Current ON core.DriverLicense (LicenseNumber) WHERE IsCurrent = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DriverDocument_DriverId' AND object_id = OBJECT_ID(N'core.DriverDocument'))
    CREATE INDEX IX_DriverDocument_DriverId ON core.DriverDocument (DriverId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DriverAvailabilityHistory_DriverId' AND object_id = OBJECT_ID(N'core.DriverAvailabilityHistory'))
    CREATE INDEX IX_DriverAvailabilityHistory_DriverId ON core.DriverAvailabilityHistory (DriverId, ChangedDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_OwnerId' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_OwnerId ON core.Vehicle (OwnerId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_Verification_Created' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_Verification_Created ON core.Vehicle (VerificationStatusId, CreatedDateUtc DESC) WHERE IsDeleted = 0;
-- Assignment screen: available vehicles of a type with enough capacity.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_Type_Available' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_Type_Available ON core.Vehicle (VehicleTypeId, IsAvailable, VerificationStatusId) INCLUDE (OwnerId, CapacityKg, VehicleNumber) WHERE IsDeleted = 0 AND IsActive = 1;
-- Compliance expiry dashboards.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_InsuranceExpiryDate' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_InsuranceExpiryDate ON core.Vehicle (InsuranceExpiryDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_PermitExpiryDate' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_PermitExpiryDate ON core.Vehicle (PermitExpiryDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicle_FitnessExpiryDate' AND object_id = OBJECT_ID(N'core.Vehicle'))
    CREATE INDEX IX_Vehicle_FitnessExpiryDate ON core.Vehicle (FitnessExpiryDate) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleDocument_VehicleId' AND object_id = OBJECT_ID(N'core.VehicleDocument'))
    CREATE INDEX IX_VehicleDocument_VehicleId ON core.VehicleDocument (VehicleId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleAvailabilityHistory_VehicleId' AND object_id = OBJECT_ID(N'core.VehicleAvailabilityHistory'))
    CREATE INDEX IX_VehicleAvailabilityHistory_VehicleId ON core.VehicleAvailabilityHistory (VehicleId, ChangedDateUtc DESC);
GO

-- ===== core: bookings / quotations =====
-- Customer portal: "my bookings" newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Booking_Customer_Created' AND object_id = OBJECT_ID(N'core.Booking'))
    CREATE INDEX IX_Booking_Customer_Created ON core.Booking (CustomerId, CreatedDateUtc DESC) INCLUDE (BookingStatusId);
-- Operations booking queue: filter by status, oldest/newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Booking_Status_Created' AND object_id = OBJECT_ID(N'core.Booking'))
    CREATE INDEX IX_Booking_Status_Created ON core.Booking (BookingStatusId, CreatedDateUtc DESC) INCLUDE (CustomerId, VehicleTypeId, RequestedPickupDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Booking_RequestedPickupDateUtc' AND object_id = OBJECT_ID(N'core.Booking'))
    CREATE INDEX IX_Booking_RequestedPickupDateUtc ON core.Booking (RequestedPickupDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingAddress_CityId' AND object_id = OBJECT_ID(N'core.BookingAddress'))
    CREATE INDEX IX_BookingAddress_CityId ON core.BookingAddress (CityId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingItem_BookingId' AND object_id = OBJECT_ID(N'core.BookingItem'))
    CREATE INDEX IX_BookingItem_BookingId ON core.BookingItem (BookingId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingNote_BookingId' AND object_id = OBJECT_ID(N'core.BookingNote'))
    CREATE INDEX IX_BookingNote_BookingId ON core.BookingNote (BookingId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BookingStatusHistory_BookingId' AND object_id = OBJECT_ID(N'core.BookingStatusHistory'))
    CREATE INDEX IX_BookingStatusHistory_BookingId ON core.BookingStatusHistory (BookingId, ChangedDateUtc);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotation_BookingId' AND object_id = OBJECT_ID(N'core.Quotation'))
    CREATE INDEX IX_Quotation_BookingId ON core.Quotation (BookingId, VersionNo DESC);
-- At most one accepted quotation per booking.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Quotation_Booking_Accepted' AND object_id = OBJECT_ID(N'core.Quotation'))
    CREATE UNIQUE INDEX UX_Quotation_Booking_Accepted ON core.Quotation (BookingId) WHERE QuotationStatusId = 3;
-- Expiry job: sent quotations by validity date.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotation_Sent_Validity' AND object_id = OBJECT_ID(N'core.Quotation'))
    CREATE INDEX IX_Quotation_Sent_Validity ON core.Quotation (ValidityDateUtc) WHERE QuotationStatusId = 2;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Quotation_Status_Created' AND object_id = OBJECT_ID(N'core.Quotation'))
    CREATE INDEX IX_Quotation_Status_Created ON core.Quotation (QuotationStatusId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuotationCharge_QuotationId' AND object_id = OBJECT_ID(N'core.QuotationCharge'))
    CREATE INDEX IX_QuotationCharge_QuotationId ON core.QuotationCharge (QuotationId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_QuotationStatusHistory_QuotationId' AND object_id = OBJECT_ID(N'core.QuotationStatusHistory'))
    CREATE INDEX IX_QuotationStatusHistory_QuotationId ON core.QuotationStatusHistory (QuotationId, ChangedDateUtc);
GO

-- ===== core: trips / tracking / pod =====
-- A booking has at most one non-cancelled trip.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Trip_Booking_Active' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE UNIQUE INDEX UX_Trip_Booking_Active ON core.Trip (BookingId) WHERE TripStatusId <> 8;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trip_BookingId' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE INDEX IX_Trip_BookingId ON core.Trip (BookingId);
-- Driver portal: "my assigned trips".
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trip_Driver_Status' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE INDEX IX_Trip_Driver_Status ON core.Trip (DriverId, TripStatusId) INCLUDE (PlannedPickupDateUtc);
-- Owner portal: "my trips" and settlements.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trip_Owner_Created' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE INDEX IX_Trip_Owner_Created ON core.Trip (OwnerId, CreatedDateUtc DESC) INCLUDE (TripStatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trip_Status_Created' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE INDEX IX_Trip_Status_Created ON core.Trip (TripStatusId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trip_VehicleId' AND object_id = OBJECT_ID(N'core.Trip'))
    CREATE INDEX IX_Trip_VehicleId ON core.Trip (VehicleId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripAssignment_TripId' AND object_id = OBJECT_ID(N'core.TripAssignment'))
    CREATE INDEX IX_TripAssignment_TripId ON core.TripAssignment (TripId, AssignedDateUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripStatusHistory_TripId' AND object_id = OBJECT_ID(N'core.TripStatusHistory'))
    CREATE INDEX IX_TripStatusHistory_TripId ON core.TripStatusHistory (TripId, ChangedDateUtc);
-- Tracking: latest point and "points since X" for a trip. The table grows fastest of all.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripLocationHistory_Trip_Recorded' AND object_id = OBJECT_ID(N'core.TripLocationHistory'))
    CREATE INDEX IX_TripLocationHistory_Trip_Recorded ON core.TripLocationHistory (TripId, RecordedDateUtc DESC) INCLUDE (Latitude, Longitude, SpeedKmph, Heading, Accuracy);
-- Active OTP lookup.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TripVerification_Trip_Type' AND object_id = OBJECT_ID(N'core.TripVerification'))
    CREATE INDEX IX_TripVerification_Trip_Type ON core.TripVerification (TripId, VerificationTypeId) WHERE IsSuperseded = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProofOfDeliveryFile_PodId' AND object_id = OBJECT_ID(N'core.ProofOfDeliveryFile'))
    CREATE INDEX IX_ProofOfDeliveryFile_PodId ON core.ProofOfDeliveryFile (ProofOfDeliveryId);
GO

-- ===== fin =====
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehiclePricing_VehicleTypeId' AND object_id = OBJECT_ID(N'fin.VehiclePricing'))
    CREATE INDEX IX_VehiclePricing_VehicleTypeId ON fin.VehiclePricing (VehicleTypeId, EffectiveFromUtc DESC) WHERE IsActive = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DistancePricing_VehicleTypeId' AND object_id = OBJECT_ID(N'fin.DistancePricing'))
    CREATE INDEX IX_DistancePricing_VehicleTypeId ON fin.DistancePricing (VehicleTypeId, FromKm) WHERE IsActive = 1;
-- One active (non-cancelled) invoice per booking.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Invoice_Booking_Active' AND object_id = OBJECT_ID(N'fin.Invoice'))
    CREATE UNIQUE INDEX UX_Invoice_Booking_Active ON fin.Invoice (BookingId) WHERE InvoiceStatusId <> 5;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoice_Customer_Date' AND object_id = OBJECT_ID(N'fin.Invoice'))
    CREATE INDEX IX_Invoice_Customer_Date ON fin.Invoice (CustomerId, InvoiceDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoice_Status_Date' AND object_id = OBJECT_ID(N'fin.Invoice'))
    CREATE INDEX IX_Invoice_Status_Date ON fin.Invoice (InvoiceStatusId, InvoiceDateUtc DESC) INCLUDE (TotalAmount, PaidAmount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceItem_InvoiceId' AND object_id = OBJECT_ID(N'fin.InvoiceItem'))
    CREATE INDEX IX_InvoiceItem_InvoiceId ON fin.InvoiceItem (InvoiceId);
-- Idempotent payment creation and webhook matching.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payment_IdempotencyKey' AND object_id = OBJECT_ID(N'fin.Payment'))
    CREATE UNIQUE INDEX UX_Payment_IdempotencyKey ON fin.Payment (CustomerId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payment_GatewayOrderId' AND object_id = OBJECT_ID(N'fin.Payment'))
    CREATE UNIQUE INDEX UX_Payment_GatewayOrderId ON fin.Payment (GatewayName, GatewayOrderId) WHERE GatewayOrderId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payment_InvoiceId' AND object_id = OBJECT_ID(N'fin.Payment'))
    CREATE INDEX IX_Payment_InvoiceId ON fin.Payment (InvoiceId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payment_Customer_Created' AND object_id = OBJECT_ID(N'fin.Payment'))
    CREATE INDEX IX_Payment_Customer_Created ON fin.Payment (CustomerId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payment_Status_Created' AND object_id = OBJECT_ID(N'fin.Payment'))
    CREATE INDEX IX_Payment_Status_Created ON fin.Payment (PaymentStatusId, CreatedDateUtc DESC) INCLUDE (Amount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PaymentAttempt_GatewayEventId' AND object_id = OBJECT_ID(N'fin.PaymentAttempt'))
    CREATE UNIQUE INDEX UX_PaymentAttempt_GatewayEventId ON fin.PaymentAttempt (GatewayEventId) WHERE GatewayEventId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentRefund_PaymentId' AND object_id = OBJECT_ID(N'fin.PaymentRefund'))
    CREATE INDEX IX_PaymentRefund_PaymentId ON fin.PaymentRefund (PaymentId);
-- One live settlement per trip (Failed = 5 and Cancelled = 6 may be retried).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Settlement_Trip_Active' AND object_id = OBJECT_ID(N'fin.Settlement'))
    CREATE UNIQUE INDEX UX_Settlement_Trip_Active ON fin.Settlement (TripId) WHERE SettlementStatusId NOT IN (5, 6);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Settlement_Owner_Created' AND object_id = OBJECT_ID(N'fin.Settlement'))
    CREATE INDEX IX_Settlement_Owner_Created ON fin.Settlement (OwnerId, CreatedDateUtc DESC) INCLUDE (SettlementStatusId, NetAmount);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Settlement_Status_Created' AND object_id = OBJECT_ID(N'fin.Settlement'))
    CREATE INDEX IX_Settlement_Status_Created ON fin.Settlement (SettlementStatusId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SettlementItem_SettlementId' AND object_id = OBJECT_ID(N'fin.SettlementItem'))
    CREATE INDEX IX_SettlementItem_SettlementId ON fin.SettlementItem (SettlementId);
GO

-- ===== sup / aud =====
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Complaint_RaisedBy' AND object_id = OBJECT_ID(N'sup.Complaint'))
    CREATE INDEX IX_Complaint_RaisedBy ON sup.Complaint (RaisedByUserId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Complaint_Status_Created' AND object_id = OBJECT_ID(N'sup.Complaint'))
    CREATE INDEX IX_Complaint_Status_Created ON sup.Complaint (ComplaintStatusId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTicket_RaisedBy' AND object_id = OBJECT_ID(N'sup.SupportTicket'))
    CREATE INDEX IX_SupportTicket_RaisedBy ON sup.SupportTicket (RaisedByUserId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTicket_Status_Priority' AND object_id = OBJECT_ID(N'sup.SupportTicket'))
    CREATE INDEX IX_SupportTicket_Status_Priority ON sup.SupportTicket (TicketStatusId, TicketPriorityId DESC, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTicketComment_TicketId' AND object_id = OBJECT_ID(N'sup.SupportTicketComment'))
    CREATE INDEX IX_SupportTicketComment_TicketId ON sup.SupportTicketComment (SupportTicketId, CreatedDateUtc);
-- Notification bell: unread for user, newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_User_Created' AND object_id = OBJECT_ID(N'sup.Notification'))
    CREATE INDEX IX_Notification_User_Created ON sup.Notification (UserId, CreatedDateUtc DESC) INCLUDE (ReadDateUtc) WHERE NotificationChannelId = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_Entity' AND object_id = OBJECT_ID(N'aud.AuditLog'))
    CREATE INDEX IX_AuditLog_Entity ON aud.AuditLog (EntityType, EntityId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_User_Created' AND object_id = OBJECT_ID(N'aud.AuditLog'))
    CREATE INDEX IX_AuditLog_User_Created ON aud.AuditLog (UserId, CreatedDateUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_CreatedDateUtc' AND object_id = OBJECT_ID(N'aud.AuditLog'))
    CREATE INDEX IX_AuditLog_CreatedDateUtc ON aud.AuditLog (CreatedDateUtc DESC);
GO
