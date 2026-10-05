/*
    End-to-end smoke test of the stored-procedure workflow, run by CI against a fresh database that
    contains 10-TestData. It drives one booking through the full lifecycle and asserts the state
    after each step. The
    test intentionally has no outer transaction: the procedures manage their own transactions and the
    negative tests rely on them rolling back cleanly. Run it once against a disposable database.
*/
SET NOCOUNT ON;
SET XACT_ABORT OFF;

DECLARE @Ops BIGINT      = (SELECT UserId FROM sec.[User] WHERE NormalizedEmail = N'OPS@PROCARGO.TEST');
DECLARE @Fin BIGINT      = (SELECT UserId FROM sec.[User] WHERE NormalizedEmail = N'FINANCE@PROCARGO.TEST');
DECLARE @CustUser BIGINT = (SELECT UserId FROM sec.[User] WHERE NormalizedEmail = N'CUSTOMER@PROCARGO.TEST');
DECLARE @DrvUser BIGINT  = (SELECT UserId FROM sec.[User] WHERE NormalizedEmail = N'DRIVER@PROCARGO.TEST');
DECLARE @CustomerId BIGINT = (SELECT CustomerId FROM core.Customer WHERE UserId = @CustUser);
DECLARE @VehicleId BIGINT  = (SELECT VehicleId FROM core.Vehicle WHERE VehicleNumber = 'KA25AB1234');
DECLARE @DriverId BIGINT   = (SELECT DriverId FROM core.Driver WHERE UserId = @DrvUser);
DECLARE @BookingId BIGINT  = (SELECT TOP (1) BookingId FROM core.Booking WHERE CustomerId = @CustomerId AND BookingStatusId = 2 ORDER BY BookingId);

IF @Ops IS NULL OR @CustomerId IS NULL OR @VehicleId IS NULL OR @DriverId IS NULL OR @BookingId IS NULL
    THROW 60000, 'Test data missing', 1;

DECLARE @Created TABLE (Id BIGINT, Number VARCHAR(30));
DECLARE @CreatedEx TABLE (Id BIGINT, Number VARCHAR(30), IsExisting BIT);
DECLARE @QuotationId BIGINT, @TripId BIGINT, @InvoiceId BIGINT, @PaymentId BIGINT, @SettlementId BIGINT, @Total DECIMAL(14,2);

-- 1. Quotation drafted -> booking moves to UnderReview
INSERT INTO @Created EXEC core.usp_Quotation_Create
    @BookingId = @BookingId, @DistanceKm = 145, @BaseAmount = 1200, @DistanceCharge = 5800, @LoadingCharge = 300, @UnloadingCharge = 300,
    @WaitingCharge = 0, @TollCharge = 450, @NightCharge = 0, @SpecialHandlingCharge = 0, @AdjustmentAmount = 0, @DiscountAmount = 0,
    @SubTotal = 8050, @TaxPercent = 5, @TaxAmount = 402.50, @TotalAmount = 8452.50, @ValidityDateUtc = '2099-01-01', @Notes = N'Smoke test',
    @ChargesJson = N'[{"chargeCode":"BASE","description":"Base fare","quantity":1,"unitRate":1200,"amount":1200,"sortOrder":1},
                      {"chargeCode":"DISTANCE","description":"Distance 145 km","quantity":145,"unitRate":40,"amount":5800,"sortOrder":2},
                      {"chargeCode":"LOADING","description":"Loading","quantity":1,"unitRate":300,"amount":300,"sortOrder":3},
                      {"chargeCode":"UNLOADING","description":"Unloading","quantity":1,"unitRate":300,"amount":300,"sortOrder":4},
                      {"chargeCode":"TOLL","description":"Tolls","quantity":1,"unitRate":450,"amount":450,"sortOrder":5}]',
    @CreatedBy = @Ops;
SELECT @QuotationId = Id FROM @Created; DELETE FROM @Created;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 3 THROW 60001, 'Expected booking UnderReview', 1;
IF (SELECT COUNT(*) FROM core.QuotationCharge WHERE QuotationId = @QuotationId) <> 5 THROW 60002, 'Expected 5 quotation charges', 1;

-- 2. Send + accept
EXEC core.usp_Quotation_Send @QuotationId = @QuotationId, @ModifiedBy = @Ops;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 4 THROW 60003, 'Expected booking Quoted', 1;
EXEC core.usp_Quotation_Accept @QuotationId = @QuotationId, @CustomerId = @CustomerId, @ModifiedBy = @CustUser;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 5 THROW 60004, 'Expected booking Confirmed', 1;

-- 3. Trip with vehicle + driver claims resources
INSERT INTO @Created EXEC core.usp_Trip_Create @BookingId = @BookingId, @VehicleId = @VehicleId, @DriverId = @DriverId,
    @PlannedPickupDateUtc = '2099-01-02T06:00:00', @PlannedDeliveryDateUtc = '2099-01-02T14:00:00', @CreatedBy = @Ops;
SELECT @TripId = Id FROM @Created; DELETE FROM @Created;
IF (SELECT IsAvailable FROM core.Vehicle WHERE VehicleId = @VehicleId) <> 0 THROW 60005, 'Vehicle should be claimed', 1;
IF (SELECT AvailabilityStatusId FROM core.Driver WHERE DriverId = @DriverId) <> 2 THROW 60006, 'Driver should be on trip', 1;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 6 THROW 60007, 'Expected booking Assigned', 1;

-- 3b. The same vehicle cannot be claimed twice
BEGIN TRY
    EXEC core.usp_Vehicle_SetAvailability @VehicleId = @VehicleId, @IsAvailable = 1, @Reason = N'should fail', @ChangedBy = @Ops;
    THROW 60008, 'Vehicle on trip must not be released manually', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50400 THROW;
END CATCH

-- 4. Pickup verified -> in transit -> delivered (vehicle/driver released)
EXEC core.usp_Trip_UpdateStatus @TripId = @TripId, @ExpectedStatusId = 1, @NewStatusId = 2, @Remarks = N'Pickup OTP verified', @ChangedBy = @DrvUser, @Odometer = 10000;
EXEC core.usp_Trip_UpdateStatus @TripId = @TripId, @ExpectedStatusId = 2, @NewStatusId = 3, @Remarks = N'Started', @ChangedBy = @DrvUser,
     @BookingExpectedStatusId = 6, @BookingNewStatusId = 7;
EXEC core.usp_Trip_UpdateStatus @TripId = @TripId, @ExpectedStatusId = 3, @NewStatusId = 4, @Remarks = N'Delivery OTP verified', @ChangedBy = @DrvUser,
     @Odometer = 10150, @BookingExpectedStatusId = 7, @BookingNewStatusId = 8;
IF (SELECT IsAvailable FROM core.Vehicle WHERE VehicleId = @VehicleId) <> 1 THROW 60009, 'Vehicle should be released', 1;
IF (SELECT AvailabilityStatusId FROM core.Driver WHERE DriverId = @DriverId) <> 1 THROW 60010, 'Driver should be available', 1;

-- 4b. Stale status change is rejected (optimistic concurrency on status)
BEGIN TRY
    EXEC core.usp_Trip_UpdateStatus @TripId = @TripId, @ExpectedStatusId = 3, @NewStatusId = 4, @Remarks = N'dup', @ChangedBy = @DrvUser;
    THROW 60011, 'Stale trip status change should fail', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50409 THROW;
END CATCH

-- 5. POD -> invoice
INSERT INTO @Created (Id) EXEC core.usp_ProofOfDelivery_Create @TripId = @TripId, @ReceiverName = N'Manjunath R', @ReceiverPhone = '+919900000011',
    @Remarks = N'Received in good condition', @Latitude = 12.3528, @Longitude = 76.6115, @SignatureFileId = NULL, @FilesJson = N'[]', @UploadedBy = @DrvUser;
DELETE FROM @Created;
IF (SELECT TripStatusId FROM core.Trip WHERE TripId = @TripId) <> 5 THROW 60012, 'Expected trip PodUploaded', 1;

INSERT INTO @Created EXEC fin.usp_Invoice_Create @BookingId = @BookingId, @PaymentTermsDays = 7, @CreatedBy = @Fin;
SELECT @InvoiceId = Id FROM @Created; DELETE FROM @Created;
SELECT @Total = TotalAmount FROM fin.Invoice WHERE InvoiceId = @InvoiceId;
IF @Total <> 8452.50 THROW 60013, 'Invoice total should equal accepted quotation', 1;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 9 THROW 60014, 'Expected booking Invoiced', 1;

-- 6. Idempotent payment: same key twice -> one payment
INSERT INTO @CreatedEx EXEC fin.usp_Payment_Create @InvoiceId = @InvoiceId, @Amount = @Total, @PaymentMethodId = 1, @PaymentStatusId = 2,
    @GatewayName = 'Sandbox', @IdempotencyKey = 'smoke-key-1', @ReferenceNumber = NULL, @Remarks = NULL, @CreatedBy = @CustUser;
INSERT INTO @CreatedEx EXEC fin.usp_Payment_Create @InvoiceId = @InvoiceId, @Amount = @Total, @PaymentMethodId = 1, @PaymentStatusId = 2,
    @GatewayName = 'Sandbox', @IdempotencyKey = 'smoke-key-1', @ReferenceNumber = NULL, @Remarks = NULL, @CreatedBy = @CustUser;
IF (SELECT COUNT(DISTINCT Id) FROM @CreatedEx) <> 1 THROW 60015, 'Idempotency key must return the same payment', 1;
SELECT TOP (1) @PaymentId = Id FROM @CreatedEx;

DECLARE @Upd TABLE (Id BIGINT, IsExisting BIT);
INSERT INTO @Upd EXEC fin.usp_Payment_UpdateStatus @PaymentId = @PaymentId, @NewStatusId = 4, @GatewayTransactionId = 'txn_smoke_1',
    @GatewayEventId = 'evt_smoke_1', @ResponseCode = '00', @ResponseMessage = N'Captured', @ModifiedBy = NULL;
INSERT INTO @Upd EXEC fin.usp_Payment_UpdateStatus @PaymentId = @PaymentId, @NewStatusId = 4, @GatewayTransactionId = 'txn_smoke_1',
    @GatewayEventId = 'evt_smoke_1', @ResponseCode = '00', @ResponseMessage = N'Captured (replayed webhook)', @ModifiedBy = NULL;
IF (SELECT COUNT(*) FROM @Upd WHERE IsExisting = 1) <> 1 THROW 60016, 'Replayed webhook must be detected', 1;
IF (SELECT InvoiceStatusId FROM fin.Invoice WHERE InvoiceId = @InvoiceId) <> 4 THROW 60017, 'Invoice should be paid', 1;
IF (SELECT PaidAmount FROM fin.Invoice WHERE InvoiceId = @InvoiceId) <> @Total THROW 60018, 'Paid amount must not be double counted', 1;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 10 THROW 60019, 'Expected booking Paid', 1;

-- 7. Settlement lifecycle closes trip and booking
INSERT INTO @Created EXEC fin.usp_Settlement_Create @TripId = @TripId, @CommissionPercent = 10, @MinimumCommission = 100, @TdsPercent = 1, @CreatedBy = @Fin;
SELECT @SettlementId = Id FROM @Created; DELETE FROM @Created;
IF (SELECT NetAmount FROM fin.Settlement WHERE SettlementId = @SettlementId) <> 7172.55 THROW 60020, 'Net = 8050 - 805 - 72.45', 1;

-- the owner has no bank account in test data: processing must be refused
EXEC fin.usp_Settlement_ChangeStatus @SettlementId = @SettlementId, @ExpectedStatusId = 1, @NewStatusId = 2, @Remarks = NULL, @ChangedBy = @Fin;
BEGIN TRY
    EXEC fin.usp_Settlement_ChangeStatus @SettlementId = @SettlementId, @ExpectedStatusId = 2, @NewStatusId = 3, @Remarks = NULL, @ChangedBy = @Fin;
    THROW 60021, 'Processing without bank account should fail', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50400 THROW;
END CATCH

INSERT INTO core.OwnerBankAccount (OwnerId, AccountHolderName, BankName, AccountNumberEncrypted, AccountNumberLast4, IfscCode, IsPrimary)
SELECT OwnerId, N'Olekar Transport', N'State Bank of India', N'ciphertext-placeholder', '4321', 'SBIN0001234', 1 FROM core.Trip WHERE TripId = @TripId;

EXEC fin.usp_Settlement_ChangeStatus @SettlementId = @SettlementId, @ExpectedStatusId = 2, @NewStatusId = 3, @Remarks = NULL, @ChangedBy = @Fin;
EXEC fin.usp_Settlement_Complete @SettlementId = @SettlementId, @TransactionReference = 'UTR-SMOKE-0001', @ChangedBy = @Fin;

IF (SELECT TripStatusId FROM core.Trip WHERE TripId = @TripId) <> 7 THROW 60022, 'Expected trip Closed', 1;
IF (SELECT BookingStatusId FROM core.Booking WHERE BookingId = @BookingId) <> 11 THROW 60023, 'Expected booking Closed', 1;

-- 8. Reads used by the API return rows
IF NOT EXISTS (SELECT 1 FROM rpt.vw_TripOverview WHERE TripId = @TripId) THROW 60024, 'Trip overview view broken', 1;

PRINT 'Workflow smoke test passed.';
GO

-- Read-only procedures must execute without errors on the seeded database.
EXEC mst.usp_Lookup_GetAll;
EXEC rpt.usp_Dashboard_GetOperationsKpis @TodayStartUtc = '2026-01-01', @MonthStartUtc = '2026-01-01';
EXEC core.usp_Booking_GetPaged @PageNumber = 1, @PageSize = 10, @Search = N'PC-BKG';
EXEC core.usp_Vehicle_GetAvailableForAssignment @VehicleTypeId = 5, @MinCapacityKg = 1000, @OnDateUtc = '2026-12-01';
EXEC rpt.usp_Report_PartnerSummary;
GO
