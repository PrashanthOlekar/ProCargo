/*
    Foreign keys, created after all tables exist so table scripts can run in any order.
    Audit columns (CreatedBy / ModifiedBy / VerifiedBy ...) intentionally have no FK to sec.[User]:
    they are written on every row and an FK would add lock/lookup cost without business value.
    All FKs use the default NO ACTION delete rule - business rows are never cascade-deleted.
*/

IF OBJECT_ID(N'mst.FK_City_StateId', N'F') IS NULL
    ALTER TABLE mst.City ADD CONSTRAINT FK_City_StateId FOREIGN KEY (StateId) REFERENCES mst.State (StateId);
IF OBJECT_ID(N'sec.FK_RolePermission_RoleId', N'F') IS NULL
    ALTER TABLE sec.RolePermission ADD CONSTRAINT FK_RolePermission_RoleId FOREIGN KEY (RoleId) REFERENCES sec.Role (RoleId);
IF OBJECT_ID(N'sec.FK_RolePermission_PermissionId', N'F') IS NULL
    ALTER TABLE sec.RolePermission ADD CONSTRAINT FK_RolePermission_PermissionId FOREIGN KEY (PermissionId) REFERENCES sec.Permission (PermissionId);
IF OBJECT_ID(N'sec.FK_UserRole_UserId', N'F') IS NULL
    ALTER TABLE sec.UserRole ADD CONSTRAINT FK_UserRole_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sec.FK_UserRole_RoleId', N'F') IS NULL
    ALTER TABLE sec.UserRole ADD CONSTRAINT FK_UserRole_RoleId FOREIGN KEY (RoleId) REFERENCES sec.Role (RoleId);
IF OBJECT_ID(N'sec.FK_RefreshToken_UserId', N'F') IS NULL
    ALTER TABLE sec.RefreshToken ADD CONSTRAINT FK_RefreshToken_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sec.FK_RefreshToken_ReplacedByTokenId', N'F') IS NULL
    ALTER TABLE sec.RefreshToken ADD CONSTRAINT FK_RefreshToken_ReplacedByTokenId FOREIGN KEY (ReplacedByTokenId) REFERENCES sec.RefreshToken (RefreshTokenId);
IF OBJECT_ID(N'sec.FK_PasswordResetToken_UserId', N'F') IS NULL
    ALTER TABLE sec.PasswordResetToken ADD CONSTRAINT FK_PasswordResetToken_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sec.FK_LoginHistory_UserId', N'F') IS NULL
    ALTER TABLE sec.LoginHistory ADD CONSTRAINT FK_LoginHistory_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'core.FK_Customer_UserId', N'F') IS NULL
    ALTER TABLE core.Customer ADD CONSTRAINT FK_Customer_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'core.FK_Customer_CustomerTypeId', N'F') IS NULL
    ALTER TABLE core.Customer ADD CONSTRAINT FK_Customer_CustomerTypeId FOREIGN KEY (CustomerTypeId) REFERENCES mst.CustomerType (CustomerTypeId);
IF OBJECT_ID(N'core.FK_CustomerAddress_CustomerId', N'F') IS NULL
    ALTER TABLE core.CustomerAddress ADD CONSTRAINT FK_CustomerAddress_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'core.FK_CustomerAddress_CityId', N'F') IS NULL
    ALTER TABLE core.CustomerAddress ADD CONSTRAINT FK_CustomerAddress_CityId FOREIGN KEY (CityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'core.FK_CustomerContact_CustomerId', N'F') IS NULL
    ALTER TABLE core.CustomerContact ADD CONSTRAINT FK_CustomerContact_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'core.FK_CustomerDocument_CustomerId', N'F') IS NULL
    ALTER TABLE core.CustomerDocument ADD CONSTRAINT FK_CustomerDocument_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'core.FK_CustomerDocument_DocumentTypeId', N'F') IS NULL
    ALTER TABLE core.CustomerDocument ADD CONSTRAINT FK_CustomerDocument_DocumentTypeId FOREIGN KEY (DocumentTypeId) REFERENCES mst.DocumentType (DocumentTypeId);
IF OBJECT_ID(N'core.FK_CustomerDocument_StoredFileId', N'F') IS NULL
    ALTER TABLE core.CustomerDocument ADD CONSTRAINT FK_CustomerDocument_StoredFileId FOREIGN KEY (StoredFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'core.FK_CustomerDocument_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.CustomerDocument ADD CONSTRAINT FK_CustomerDocument_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_VehicleOwner_UserId', N'F') IS NULL
    ALTER TABLE core.VehicleOwner ADD CONSTRAINT FK_VehicleOwner_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'core.FK_VehicleOwner_OwnerTypeId', N'F') IS NULL
    ALTER TABLE core.VehicleOwner ADD CONSTRAINT FK_VehicleOwner_OwnerTypeId FOREIGN KEY (OwnerTypeId) REFERENCES mst.OwnerType (OwnerTypeId);
IF OBJECT_ID(N'core.FK_VehicleOwner_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.VehicleOwner ADD CONSTRAINT FK_VehicleOwner_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_OwnerBusiness_OwnerId', N'F') IS NULL
    ALTER TABLE core.OwnerBusiness ADD CONSTRAINT FK_OwnerBusiness_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_OwnerAddress_OwnerId', N'F') IS NULL
    ALTER TABLE core.OwnerAddress ADD CONSTRAINT FK_OwnerAddress_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_OwnerAddress_CityId', N'F') IS NULL
    ALTER TABLE core.OwnerAddress ADD CONSTRAINT FK_OwnerAddress_CityId FOREIGN KEY (CityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'core.FK_OwnerBankAccount_OwnerId', N'F') IS NULL
    ALTER TABLE core.OwnerBankAccount ADD CONSTRAINT FK_OwnerBankAccount_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_OwnerBankAccount_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.OwnerBankAccount ADD CONSTRAINT FK_OwnerBankAccount_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_OwnerDocument_OwnerId', N'F') IS NULL
    ALTER TABLE core.OwnerDocument ADD CONSTRAINT FK_OwnerDocument_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_OwnerDocument_DocumentTypeId', N'F') IS NULL
    ALTER TABLE core.OwnerDocument ADD CONSTRAINT FK_OwnerDocument_DocumentTypeId FOREIGN KEY (DocumentTypeId) REFERENCES mst.DocumentType (DocumentTypeId);
IF OBJECT_ID(N'core.FK_OwnerDocument_StoredFileId', N'F') IS NULL
    ALTER TABLE core.OwnerDocument ADD CONSTRAINT FK_OwnerDocument_StoredFileId FOREIGN KEY (StoredFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'core.FK_OwnerDocument_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.OwnerDocument ADD CONSTRAINT FK_OwnerDocument_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_Driver_UserId', N'F') IS NULL
    ALTER TABLE core.Driver ADD CONSTRAINT FK_Driver_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'core.FK_Driver_OwnerId', N'F') IS NULL
    ALTER TABLE core.Driver ADD CONSTRAINT FK_Driver_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_Driver_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.Driver ADD CONSTRAINT FK_Driver_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_Driver_AvailabilityStatusId', N'F') IS NULL
    ALTER TABLE core.Driver ADD CONSTRAINT FK_Driver_AvailabilityStatusId FOREIGN KEY (AvailabilityStatusId) REFERENCES mst.DriverAvailabilityStatus (DriverAvailabilityStatusId);
IF OBJECT_ID(N'core.FK_DriverLicense_DriverId', N'F') IS NULL
    ALTER TABLE core.DriverLicense ADD CONSTRAINT FK_DriverLicense_DriverId FOREIGN KEY (DriverId) REFERENCES core.Driver (DriverId);
IF OBJECT_ID(N'core.FK_DriverDocument_DriverId', N'F') IS NULL
    ALTER TABLE core.DriverDocument ADD CONSTRAINT FK_DriverDocument_DriverId FOREIGN KEY (DriverId) REFERENCES core.Driver (DriverId);
IF OBJECT_ID(N'core.FK_DriverDocument_DocumentTypeId', N'F') IS NULL
    ALTER TABLE core.DriverDocument ADD CONSTRAINT FK_DriverDocument_DocumentTypeId FOREIGN KEY (DocumentTypeId) REFERENCES mst.DocumentType (DocumentTypeId);
IF OBJECT_ID(N'core.FK_DriverDocument_StoredFileId', N'F') IS NULL
    ALTER TABLE core.DriverDocument ADD CONSTRAINT FK_DriverDocument_StoredFileId FOREIGN KEY (StoredFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'core.FK_DriverDocument_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.DriverDocument ADD CONSTRAINT FK_DriverDocument_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_DriverAvailabilityHistory_DriverId', N'F') IS NULL
    ALTER TABLE core.DriverAvailabilityHistory ADD CONSTRAINT FK_DriverAvailabilityHistory_DriverId FOREIGN KEY (DriverId) REFERENCES core.Driver (DriverId);
IF OBJECT_ID(N'core.FK_DriverAvailabilityHistory_AvailabilityStatusId', N'F') IS NULL
    ALTER TABLE core.DriverAvailabilityHistory ADD CONSTRAINT FK_DriverAvailabilityHistory_AvailabilityStatusId FOREIGN KEY (AvailabilityStatusId) REFERENCES mst.DriverAvailabilityStatus (DriverAvailabilityStatusId);
IF OBJECT_ID(N'core.FK_Vehicle_OwnerId', N'F') IS NULL
    ALTER TABLE core.Vehicle ADD CONSTRAINT FK_Vehicle_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_Vehicle_VehicleTypeId', N'F') IS NULL
    ALTER TABLE core.Vehicle ADD CONSTRAINT FK_Vehicle_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'core.FK_Vehicle_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.Vehicle ADD CONSTRAINT FK_Vehicle_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_VehicleDocument_VehicleId', N'F') IS NULL
    ALTER TABLE core.VehicleDocument ADD CONSTRAINT FK_VehicleDocument_VehicleId FOREIGN KEY (VehicleId) REFERENCES core.Vehicle (VehicleId);
IF OBJECT_ID(N'core.FK_VehicleDocument_DocumentTypeId', N'F') IS NULL
    ALTER TABLE core.VehicleDocument ADD CONSTRAINT FK_VehicleDocument_DocumentTypeId FOREIGN KEY (DocumentTypeId) REFERENCES mst.DocumentType (DocumentTypeId);
IF OBJECT_ID(N'core.FK_VehicleDocument_StoredFileId', N'F') IS NULL
    ALTER TABLE core.VehicleDocument ADD CONSTRAINT FK_VehicleDocument_StoredFileId FOREIGN KEY (StoredFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'core.FK_VehicleDocument_VerificationStatusId', N'F') IS NULL
    ALTER TABLE core.VehicleDocument ADD CONSTRAINT FK_VehicleDocument_VerificationStatusId FOREIGN KEY (VerificationStatusId) REFERENCES mst.VerificationStatus (VerificationStatusId);
IF OBJECT_ID(N'core.FK_VehicleAvailabilityHistory_VehicleId', N'F') IS NULL
    ALTER TABLE core.VehicleAvailabilityHistory ADD CONSTRAINT FK_VehicleAvailabilityHistory_VehicleId FOREIGN KEY (VehicleId) REFERENCES core.Vehicle (VehicleId);
IF OBJECT_ID(N'core.FK_Booking_CustomerId', N'F') IS NULL
    ALTER TABLE core.Booking ADD CONSTRAINT FK_Booking_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'core.FK_Booking_VehicleTypeId', N'F') IS NULL
    ALTER TABLE core.Booking ADD CONSTRAINT FK_Booking_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'core.FK_Booking_GoodsTypeId', N'F') IS NULL
    ALTER TABLE core.Booking ADD CONSTRAINT FK_Booking_GoodsTypeId FOREIGN KEY (GoodsTypeId) REFERENCES mst.GoodsType (GoodsTypeId);
IF OBJECT_ID(N'core.FK_Booking_BookingStatusId', N'F') IS NULL
    ALTER TABLE core.Booking ADD CONSTRAINT FK_Booking_BookingStatusId FOREIGN KEY (BookingStatusId) REFERENCES mst.BookingStatus (BookingStatusId);
IF OBJECT_ID(N'core.FK_BookingAddress_BookingId', N'F') IS NULL
    ALTER TABLE core.BookingAddress ADD CONSTRAINT FK_BookingAddress_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_BookingAddress_CityId', N'F') IS NULL
    ALTER TABLE core.BookingAddress ADD CONSTRAINT FK_BookingAddress_CityId FOREIGN KEY (CityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'core.FK_BookingContact_BookingId', N'F') IS NULL
    ALTER TABLE core.BookingContact ADD CONSTRAINT FK_BookingContact_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_BookingItem_BookingId', N'F') IS NULL
    ALTER TABLE core.BookingItem ADD CONSTRAINT FK_BookingItem_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_BookingNote_BookingId', N'F') IS NULL
    ALTER TABLE core.BookingNote ADD CONSTRAINT FK_BookingNote_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_BookingStatusHistory_BookingId', N'F') IS NULL
    ALTER TABLE core.BookingStatusHistory ADD CONSTRAINT FK_BookingStatusHistory_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_BookingStatusHistory_ToStatusId', N'F') IS NULL
    ALTER TABLE core.BookingStatusHistory ADD CONSTRAINT FK_BookingStatusHistory_ToStatusId FOREIGN KEY (ToStatusId) REFERENCES mst.BookingStatus (BookingStatusId);
IF OBJECT_ID(N'core.FK_Quotation_BookingId', N'F') IS NULL
    ALTER TABLE core.Quotation ADD CONSTRAINT FK_Quotation_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_Quotation_QuotationStatusId', N'F') IS NULL
    ALTER TABLE core.Quotation ADD CONSTRAINT FK_Quotation_QuotationStatusId FOREIGN KEY (QuotationStatusId) REFERENCES mst.QuotationStatus (QuotationStatusId);
IF OBJECT_ID(N'core.FK_QuotationCharge_QuotationId', N'F') IS NULL
    ALTER TABLE core.QuotationCharge ADD CONSTRAINT FK_QuotationCharge_QuotationId FOREIGN KEY (QuotationId) REFERENCES core.Quotation (QuotationId);
IF OBJECT_ID(N'core.FK_QuotationStatusHistory_QuotationId', N'F') IS NULL
    ALTER TABLE core.QuotationStatusHistory ADD CONSTRAINT FK_QuotationStatusHistory_QuotationId FOREIGN KEY (QuotationId) REFERENCES core.Quotation (QuotationId);
IF OBJECT_ID(N'core.FK_Trip_BookingId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'core.FK_Trip_QuotationId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_QuotationId FOREIGN KEY (QuotationId) REFERENCES core.Quotation (QuotationId);
IF OBJECT_ID(N'core.FK_Trip_VehicleId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_VehicleId FOREIGN KEY (VehicleId) REFERENCES core.Vehicle (VehicleId);
IF OBJECT_ID(N'core.FK_Trip_DriverId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_DriverId FOREIGN KEY (DriverId) REFERENCES core.Driver (DriverId);
IF OBJECT_ID(N'core.FK_Trip_OwnerId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'core.FK_Trip_TripStatusId', N'F') IS NULL
    ALTER TABLE core.Trip ADD CONSTRAINT FK_Trip_TripStatusId FOREIGN KEY (TripStatusId) REFERENCES mst.TripStatus (TripStatusId);
IF OBJECT_ID(N'core.FK_TripAssignment_TripId', N'F') IS NULL
    ALTER TABLE core.TripAssignment ADD CONSTRAINT FK_TripAssignment_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'core.FK_TripAssignment_VehicleId', N'F') IS NULL
    ALTER TABLE core.TripAssignment ADD CONSTRAINT FK_TripAssignment_VehicleId FOREIGN KEY (VehicleId) REFERENCES core.Vehicle (VehicleId);
IF OBJECT_ID(N'core.FK_TripAssignment_DriverId', N'F') IS NULL
    ALTER TABLE core.TripAssignment ADD CONSTRAINT FK_TripAssignment_DriverId FOREIGN KEY (DriverId) REFERENCES core.Driver (DriverId);
IF OBJECT_ID(N'core.FK_TripStatusHistory_TripId', N'F') IS NULL
    ALTER TABLE core.TripStatusHistory ADD CONSTRAINT FK_TripStatusHistory_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'core.FK_TripStatusHistory_ToStatusId', N'F') IS NULL
    ALTER TABLE core.TripStatusHistory ADD CONSTRAINT FK_TripStatusHistory_ToStatusId FOREIGN KEY (ToStatusId) REFERENCES mst.TripStatus (TripStatusId);
IF OBJECT_ID(N'core.FK_TripLocationHistory_TripId', N'F') IS NULL
    ALTER TABLE core.TripLocationHistory ADD CONSTRAINT FK_TripLocationHistory_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'core.FK_TripLocationHistory_TrackingProviderId', N'F') IS NULL
    ALTER TABLE core.TripLocationHistory ADD CONSTRAINT FK_TripLocationHistory_TrackingProviderId FOREIGN KEY (TrackingProviderId) REFERENCES core.TrackingProvider (TrackingProviderId);
IF OBJECT_ID(N'core.FK_TripVerification_TripId', N'F') IS NULL
    ALTER TABLE core.TripVerification ADD CONSTRAINT FK_TripVerification_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'core.FK_ProofOfDelivery_TripId', N'F') IS NULL
    ALTER TABLE core.ProofOfDelivery ADD CONSTRAINT FK_ProofOfDelivery_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'core.FK_ProofOfDelivery_SignatureFileId', N'F') IS NULL
    ALTER TABLE core.ProofOfDelivery ADD CONSTRAINT FK_ProofOfDelivery_SignatureFileId FOREIGN KEY (SignatureFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'core.FK_ProofOfDeliveryFile_ProofOfDeliveryId', N'F') IS NULL
    ALTER TABLE core.ProofOfDeliveryFile ADD CONSTRAINT FK_ProofOfDeliveryFile_ProofOfDeliveryId FOREIGN KEY (ProofOfDeliveryId) REFERENCES core.ProofOfDelivery (ProofOfDeliveryId);
IF OBJECT_ID(N'core.FK_ProofOfDeliveryFile_StoredFileId', N'F') IS NULL
    ALTER TABLE core.ProofOfDeliveryFile ADD CONSTRAINT FK_ProofOfDeliveryFile_StoredFileId FOREIGN KEY (StoredFileId) REFERENCES core.StoredFile (StoredFileId);
IF OBJECT_ID(N'fin.FK_VehiclePricing_VehicleTypeId', N'F') IS NULL
    ALTER TABLE fin.VehiclePricing ADD CONSTRAINT FK_VehiclePricing_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'fin.FK_DistancePricing_VehicleTypeId', N'F') IS NULL
    ALTER TABLE fin.DistancePricing ADD CONSTRAINT FK_DistancePricing_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'fin.FK_AdditionalCharge_VehicleTypeId', N'F') IS NULL
    ALTER TABLE fin.AdditionalCharge ADD CONSTRAINT FK_AdditionalCharge_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'fin.FK_PricingRule_VehicleTypeId', N'F') IS NULL
    ALTER TABLE fin.PricingRule ADD CONSTRAINT FK_PricingRule_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'fin.FK_PricingRule_StateId', N'F') IS NULL
    ALTER TABLE fin.PricingRule ADD CONSTRAINT FK_PricingRule_StateId FOREIGN KEY (StateId) REFERENCES mst.State (StateId);
IF OBJECT_ID(N'fin.FK_PricingRule_CityId', N'F') IS NULL
    ALTER TABLE fin.PricingRule ADD CONSTRAINT FK_PricingRule_CityId FOREIGN KEY (CityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'fin.FK_PricingRule_PickupCityId', N'F') IS NULL
    ALTER TABLE fin.PricingRule ADD CONSTRAINT FK_PricingRule_PickupCityId FOREIGN KEY (PickupCityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'fin.FK_PricingRule_DeliveryCityId', N'F') IS NULL
    ALTER TABLE fin.PricingRule ADD CONSTRAINT FK_PricingRule_DeliveryCityId FOREIGN KEY (DeliveryCityId) REFERENCES mst.City (CityId);
IF OBJECT_ID(N'fin.FK_CommissionRule_VehicleTypeId', N'F') IS NULL
    ALTER TABLE fin.CommissionRule ADD CONSTRAINT FK_CommissionRule_VehicleTypeId FOREIGN KEY (VehicleTypeId) REFERENCES mst.VehicleType (VehicleTypeId);
IF OBJECT_ID(N'fin.FK_Invoice_BookingId', N'F') IS NULL
    ALTER TABLE fin.Invoice ADD CONSTRAINT FK_Invoice_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'fin.FK_Invoice_TripId', N'F') IS NULL
    ALTER TABLE fin.Invoice ADD CONSTRAINT FK_Invoice_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'fin.FK_Invoice_CustomerId', N'F') IS NULL
    ALTER TABLE fin.Invoice ADD CONSTRAINT FK_Invoice_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'fin.FK_Invoice_QuotationId', N'F') IS NULL
    ALTER TABLE fin.Invoice ADD CONSTRAINT FK_Invoice_QuotationId FOREIGN KEY (QuotationId) REFERENCES core.Quotation (QuotationId);
IF OBJECT_ID(N'fin.FK_Invoice_InvoiceStatusId', N'F') IS NULL
    ALTER TABLE fin.Invoice ADD CONSTRAINT FK_Invoice_InvoiceStatusId FOREIGN KEY (InvoiceStatusId) REFERENCES mst.InvoiceStatus (InvoiceStatusId);
IF OBJECT_ID(N'fin.FK_InvoiceItem_InvoiceId', N'F') IS NULL
    ALTER TABLE fin.InvoiceItem ADD CONSTRAINT FK_InvoiceItem_InvoiceId FOREIGN KEY (InvoiceId) REFERENCES fin.Invoice (InvoiceId);
IF OBJECT_ID(N'fin.FK_Payment_InvoiceId', N'F') IS NULL
    ALTER TABLE fin.Payment ADD CONSTRAINT FK_Payment_InvoiceId FOREIGN KEY (InvoiceId) REFERENCES fin.Invoice (InvoiceId);
IF OBJECT_ID(N'fin.FK_Payment_BookingId', N'F') IS NULL
    ALTER TABLE fin.Payment ADD CONSTRAINT FK_Payment_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'fin.FK_Payment_CustomerId', N'F') IS NULL
    ALTER TABLE fin.Payment ADD CONSTRAINT FK_Payment_CustomerId FOREIGN KEY (CustomerId) REFERENCES core.Customer (CustomerId);
IF OBJECT_ID(N'fin.FK_Payment_PaymentMethodId', N'F') IS NULL
    ALTER TABLE fin.Payment ADD CONSTRAINT FK_Payment_PaymentMethodId FOREIGN KEY (PaymentMethodId) REFERENCES mst.PaymentMethod (PaymentMethodId);
IF OBJECT_ID(N'fin.FK_Payment_PaymentStatusId', N'F') IS NULL
    ALTER TABLE fin.Payment ADD CONSTRAINT FK_Payment_PaymentStatusId FOREIGN KEY (PaymentStatusId) REFERENCES mst.PaymentStatus (PaymentStatusId);
IF OBJECT_ID(N'fin.FK_PaymentAttempt_PaymentId', N'F') IS NULL
    ALTER TABLE fin.PaymentAttempt ADD CONSTRAINT FK_PaymentAttempt_PaymentId FOREIGN KEY (PaymentId) REFERENCES fin.Payment (PaymentId);
IF OBJECT_ID(N'fin.FK_PaymentRefund_PaymentId', N'F') IS NULL
    ALTER TABLE fin.PaymentRefund ADD CONSTRAINT FK_PaymentRefund_PaymentId FOREIGN KEY (PaymentId) REFERENCES fin.Payment (PaymentId);
IF OBJECT_ID(N'fin.FK_PaymentRefund_RefundStatusId', N'F') IS NULL
    ALTER TABLE fin.PaymentRefund ADD CONSTRAINT FK_PaymentRefund_RefundStatusId FOREIGN KEY (RefundStatusId) REFERENCES mst.RefundStatus (RefundStatusId);
IF OBJECT_ID(N'fin.FK_Settlement_TripId', N'F') IS NULL
    ALTER TABLE fin.Settlement ADD CONSTRAINT FK_Settlement_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'fin.FK_Settlement_OwnerId', N'F') IS NULL
    ALTER TABLE fin.Settlement ADD CONSTRAINT FK_Settlement_OwnerId FOREIGN KEY (OwnerId) REFERENCES core.VehicleOwner (OwnerId);
IF OBJECT_ID(N'fin.FK_Settlement_OwnerBankAccountId', N'F') IS NULL
    ALTER TABLE fin.Settlement ADD CONSTRAINT FK_Settlement_OwnerBankAccountId FOREIGN KEY (OwnerBankAccountId) REFERENCES core.OwnerBankAccount (OwnerBankAccountId);
IF OBJECT_ID(N'fin.FK_Settlement_SettlementStatusId', N'F') IS NULL
    ALTER TABLE fin.Settlement ADD CONSTRAINT FK_Settlement_SettlementStatusId FOREIGN KEY (SettlementStatusId) REFERENCES mst.SettlementStatus (SettlementStatusId);
IF OBJECT_ID(N'fin.FK_SettlementItem_SettlementId', N'F') IS NULL
    ALTER TABLE fin.SettlementItem ADD CONSTRAINT FK_SettlementItem_SettlementId FOREIGN KEY (SettlementId) REFERENCES fin.Settlement (SettlementId);
IF OBJECT_ID(N'sup.FK_Complaint_RaisedByUserId', N'F') IS NULL
    ALTER TABLE sup.Complaint ADD CONSTRAINT FK_Complaint_RaisedByUserId FOREIGN KEY (RaisedByUserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sup.FK_Complaint_AssignedToUserId', N'F') IS NULL
    ALTER TABLE sup.Complaint ADD CONSTRAINT FK_Complaint_AssignedToUserId FOREIGN KEY (AssignedToUserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sup.FK_Complaint_BookingId', N'F') IS NULL
    ALTER TABLE sup.Complaint ADD CONSTRAINT FK_Complaint_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'sup.FK_Complaint_TripId', N'F') IS NULL
    ALTER TABLE sup.Complaint ADD CONSTRAINT FK_Complaint_TripId FOREIGN KEY (TripId) REFERENCES core.Trip (TripId);
IF OBJECT_ID(N'sup.FK_Complaint_ComplaintStatusId', N'F') IS NULL
    ALTER TABLE sup.Complaint ADD CONSTRAINT FK_Complaint_ComplaintStatusId FOREIGN KEY (ComplaintStatusId) REFERENCES mst.ComplaintStatus (ComplaintStatusId);
IF OBJECT_ID(N'sup.FK_SupportTicket_RaisedByUserId', N'F') IS NULL
    ALTER TABLE sup.SupportTicket ADD CONSTRAINT FK_SupportTicket_RaisedByUserId FOREIGN KEY (RaisedByUserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sup.FK_SupportTicket_AssignedToUserId', N'F') IS NULL
    ALTER TABLE sup.SupportTicket ADD CONSTRAINT FK_SupportTicket_AssignedToUserId FOREIGN KEY (AssignedToUserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sup.FK_SupportTicket_BookingId', N'F') IS NULL
    ALTER TABLE sup.SupportTicket ADD CONSTRAINT FK_SupportTicket_BookingId FOREIGN KEY (BookingId) REFERENCES core.Booking (BookingId);
IF OBJECT_ID(N'sup.FK_SupportTicket_TicketPriorityId', N'F') IS NULL
    ALTER TABLE sup.SupportTicket ADD CONSTRAINT FK_SupportTicket_TicketPriorityId FOREIGN KEY (TicketPriorityId) REFERENCES mst.TicketPriority (TicketPriorityId);
IF OBJECT_ID(N'sup.FK_SupportTicket_TicketStatusId', N'F') IS NULL
    ALTER TABLE sup.SupportTicket ADD CONSTRAINT FK_SupportTicket_TicketStatusId FOREIGN KEY (TicketStatusId) REFERENCES mst.TicketStatus (TicketStatusId);
IF OBJECT_ID(N'sup.FK_SupportTicketComment_SupportTicketId', N'F') IS NULL
    ALTER TABLE sup.SupportTicketComment ADD CONSTRAINT FK_SupportTicketComment_SupportTicketId FOREIGN KEY (SupportTicketId) REFERENCES sup.SupportTicket (SupportTicketId);
IF OBJECT_ID(N'sup.FK_NotificationTemplate_NotificationChannelId', N'F') IS NULL
    ALTER TABLE sup.NotificationTemplate ADD CONSTRAINT FK_NotificationTemplate_NotificationChannelId FOREIGN KEY (NotificationChannelId) REFERENCES mst.NotificationChannel (NotificationChannelId);
IF OBJECT_ID(N'sup.FK_Notification_UserId', N'F') IS NULL
    ALTER TABLE sup.Notification ADD CONSTRAINT FK_Notification_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
IF OBJECT_ID(N'sup.FK_Notification_NotificationChannelId', N'F') IS NULL
    ALTER TABLE sup.Notification ADD CONSTRAINT FK_Notification_NotificationChannelId FOREIGN KEY (NotificationChannelId) REFERENCES mst.NotificationChannel (NotificationChannelId);
IF OBJECT_ID(N'sup.FK_Notification_NotificationStatusId', N'F') IS NULL
    ALTER TABLE sup.Notification ADD CONSTRAINT FK_Notification_NotificationStatusId FOREIGN KEY (NotificationStatusId) REFERENCES mst.NotificationStatus (NotificationStatusId);
IF OBJECT_ID(N'aud.FK_AuditLog_UserId', N'F') IS NULL
    ALTER TABLE aud.AuditLog ADD CONSTRAINT FK_AuditLog_UserId FOREIGN KEY (UserId) REFERENCES sec.[User] (UserId);
GO
