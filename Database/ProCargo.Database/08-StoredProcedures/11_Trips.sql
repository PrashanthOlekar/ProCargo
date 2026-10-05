/*
    Module: Trips, assignments, OTP verification, tracking and proof of delivery (schema core)
    Trip status: 1 Scheduled, 2 PickupVerified, 3 InTransit, 4 Delivered, 5 PodUploaded, 6 Completed,
                 7 Closed, 8 Cancelled, 9 OnHold, 10 Exception
    "Active" trip statuses that hold a vehicle/driver: 1, 2, 3, 9, 10.

    Vehicle/driver claiming is done with a conditional UPDATE (IsAvailable = 1 / AvailabilityStatusId = 1)
    inside the transaction, so two dispatchers can never assign the same truck or driver at the same time.
*/

CREATE OR ALTER PROCEDURE core.usp_Trip_Create
    @BookingId               BIGINT,
    @VehicleId               BIGINT,
    @DriverId                BIGINT,
    @PlannedPickupDateUtc    DATETIME2(3),
    @PlannedDeliveryDateUtc  DATETIME2(3),
    @CreatedBy               BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @TripId BIGINT, @TripNumber VARCHAR(30), @QuotationId BIGINT, @OwnerId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM core.Booking WITH (UPDLOCK) WHERE BookingId = @BookingId AND BookingStatusId = 5)
            THROW 50400, N'BOOKING_NOT_CONFIRMED|A trip can only be created for a confirmed booking.', 1;

        SELECT @QuotationId = QuotationId FROM core.Quotation WHERE BookingId = @BookingId AND QuotationStatusId = 3;
        IF @QuotationId IS NULL
            THROW 50400, N'QUOTATION_NOT_ACCEPTED|The booking has no accepted quotation.', 1;

        -- claim the vehicle
        UPDATE core.Vehicle
        SET @OwnerId = OwnerId, IsAvailable = 0, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now
        WHERE VehicleId = @VehicleId AND IsAvailable = 1 AND IsActive = 1 AND IsDeleted = 0 AND VerificationStatusId = 3;

        IF @@ROWCOUNT = 0
            THROW 50409, N'VEHICLE_NOT_AVAILABLE|The vehicle is not available for assignment.', 1;

        -- claim the driver
        UPDATE core.Driver
        SET AvailabilityStatusId = 2, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now
        WHERE DriverId = @DriverId AND AvailabilityStatusId = 1 AND IsActive = 1 AND IsDeleted = 0 AND VerificationStatusId = 3;

        IF @@ROWCOUNT = 0
            THROW 50409, N'DRIVER_NOT_AVAILABLE|The driver is not available for assignment.', 1;

        SET @TripNumber = core.fn_FormatBusinessNumber('TRP', NEXT VALUE FOR core.seq_TripNumber, @Now);

        INSERT INTO core.Trip (TripNumber, BookingId, QuotationId, VehicleId, DriverId, OwnerId, TripStatusId, PlannedPickupDateUtc, PlannedDeliveryDateUtc, CreatedBy, CreatedDateUtc)
        VALUES (@TripNumber, @BookingId, @QuotationId, @VehicleId, @DriverId, @OwnerId, 1, @PlannedPickupDateUtc, @PlannedDeliveryDateUtc, @CreatedBy, @Now);
        SET @TripId = SCOPE_IDENTITY();

        INSERT INTO core.TripAssignment (TripId, AssignmentTypeId, VehicleId, DriverId, AssignedBy, AssignedDateUtc)
        VALUES (@TripId, 1, @VehicleId, NULL, @CreatedBy, @Now),
               (@TripId, 2, NULL, @DriverId, @CreatedBy, @Now);

        INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@TripId, NULL, 1, N'Trip scheduled', @CreatedBy, @Now);

        INSERT INTO core.VehicleAvailabilityHistory (VehicleId, IsAvailable, Reason, ChangedBy, ChangedDateUtc)
        VALUES (@VehicleId, 0, N'Assigned to trip ' + @TripNumber, @CreatedBy, @Now);

        INSERT INTO core.DriverAvailabilityHistory (DriverId, AvailabilityStatusId, Reason, ChangedBy, ChangedDateUtc)
        VALUES (@DriverId, 2, N'Assigned to trip ' + @TripNumber, @CreatedBy, @Now);

        UPDATE core.Booking SET BookingStatusId = 6, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, 5, 6, N'Vehicle and driver assigned (' + @TripNumber + N')', @CreatedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @TripId AS Id, @TripNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_AssignVehicle
    @TripId      BIGINT,
    @VehicleId   BIGINT,
    @Reason      NVARCHAR(300),
    @AssignedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @OldVehicleId BIGINT, @NewOwnerId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @OldVehicleId = VehicleId FROM core.Trip WITH (UPDLOCK) WHERE TripId = @TripId AND TripStatusId = 1;
        IF @OldVehicleId IS NULL
            THROW 50400, N'TRIP_NOT_REASSIGNABLE|Vehicles can only be changed before pickup.', 1;
        IF @OldVehicleId = @VehicleId
            THROW 50400, N'VEHICLE_ALREADY_ASSIGNED|This vehicle is already assigned to the trip.', 1;

        UPDATE core.Vehicle
        SET @NewOwnerId = OwnerId, IsAvailable = 0, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now
        WHERE VehicleId = @VehicleId AND IsAvailable = 1 AND IsActive = 1 AND IsDeleted = 0 AND VerificationStatusId = 3;

        IF @@ROWCOUNT = 0
            THROW 50409, N'VEHICLE_NOT_AVAILABLE|The vehicle is not available for assignment.', 1;

        UPDATE core.Vehicle SET IsAvailable = 1, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now
        WHERE VehicleId = @OldVehicleId AND IsActive = 1 AND VerificationStatusId = 3;

        UPDATE core.TripAssignment SET ReleasedDateUtc = @Now
        WHERE TripId = @TripId AND AssignmentTypeId = 1 AND ReleasedDateUtc IS NULL;

        INSERT INTO core.TripAssignment (TripId, AssignmentTypeId, VehicleId, AssignedBy, AssignedDateUtc, Reason)
        VALUES (@TripId, 1, @VehicleId, @AssignedBy, @Now, @Reason);

        UPDATE core.Trip SET VehicleId = @VehicleId, OwnerId = @NewOwnerId, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now
        WHERE TripId = @TripId;

        INSERT INTO core.VehicleAvailabilityHistory (VehicleId, IsAvailable, Reason, ChangedBy, ChangedDateUtc)
        VALUES (@OldVehicleId, 1, N'Released from trip', @AssignedBy, @Now),
               (@VehicleId, 0, N'Assigned to trip', @AssignedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_AssignDriver
    @TripId      BIGINT,
    @DriverId    BIGINT,
    @Reason      NVARCHAR(300),
    @AssignedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @OldDriverId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @OldDriverId = DriverId FROM core.Trip WITH (UPDLOCK) WHERE TripId = @TripId AND TripStatusId = 1;
        IF @OldDriverId IS NULL
            THROW 50400, N'TRIP_NOT_REASSIGNABLE|Drivers can only be changed before pickup.', 1;
        IF @OldDriverId = @DriverId
            THROW 50400, N'DRIVER_ALREADY_ASSIGNED|This driver is already assigned to the trip.', 1;

        UPDATE core.Driver
        SET AvailabilityStatusId = 2, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now
        WHERE DriverId = @DriverId AND AvailabilityStatusId = 1 AND IsActive = 1 AND IsDeleted = 0 AND VerificationStatusId = 3;

        IF @@ROWCOUNT = 0
            THROW 50409, N'DRIVER_NOT_AVAILABLE|The driver is not available for assignment.', 1;

        UPDATE core.Driver SET AvailabilityStatusId = 1, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now
        WHERE DriverId = @OldDriverId AND AvailabilityStatusId = 2;

        UPDATE core.TripAssignment SET ReleasedDateUtc = @Now
        WHERE TripId = @TripId AND AssignmentTypeId = 2 AND ReleasedDateUtc IS NULL;

        INSERT INTO core.TripAssignment (TripId, AssignmentTypeId, DriverId, AssignedBy, AssignedDateUtc, Reason)
        VALUES (@TripId, 2, @DriverId, @AssignedBy, @Now, @Reason);

        UPDATE core.Trip SET DriverId = @DriverId, ModifiedBy = @AssignedBy, ModifiedDateUtc = @Now WHERE TripId = @TripId;

        INSERT INTO core.DriverAvailabilityHistory (DriverId, AvailabilityStatusId, Reason, ChangedBy, ChangedDateUtc)
        VALUES (@OldDriverId, 1, N'Released from trip', @AssignedBy, @Now),
               (@DriverId, 2, N'Assigned to trip', @AssignedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_GetById
    @TripId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.TripId,
        t.TripNumber,
        t.BookingId,
        b.BookingNumber,
        b.CustomerId,
        c.FullName              AS CustomerName,
        t.QuotationId,
        t.VehicleId,
        v.VehicleNumber,
        vt.Name                 AS VehicleTypeName,
        t.DriverId,
        d.FullName              AS DriverName,
        d.PhoneNumber           AS DriverPhoneNumber,
        t.OwnerId,
        o.FullName              AS OwnerName,
        t.TripStatusId,
        t.StatusBeforeHoldId,
        t.PlannedPickupDateUtc,
        t.ActualPickupDateUtc,
        t.PlannedDeliveryDateUtc,
        t.ActualDeliveryDateUtc,
        t.StartOdometer,
        t.EndOdometer,
        t.ExceptionReason,
        t.CancellationReason,
        CONCAT_WS(N', ', pa.AddressLine1, pa.AddressLine2, pa.Landmark, pc.Name, pa.Pincode) AS PickupAddress,
        pc.Name                 AS PickupCityName,
        pa.Latitude             AS PickupLatitude,
        pa.Longitude            AS PickupLongitude,
        CONCAT_WS(N', ', da.AddressLine1, da.AddressLine2, da.Landmark, dc.Name, da.Pincode) AS DeliveryAddress,
        dc.Name                 AS DeliveryCityName,
        da.Latitude             AS DeliveryLatitude,
        da.Longitude            AS DeliveryLongitude,
        pct.ContactName         AS PickupContactName,
        pct.PhoneNumber         AS PickupContactPhone,
        dct.ContactName         AS DeliveryContactName,
        dct.PhoneNumber         AS DeliveryContactPhone,
        b.GoodsDescription,
        b.TotalWeightKg,
        b.TotalQuantity,
        b.SpecialInstructions,
        CAST(CASE WHEN pod.ProofOfDeliveryId IS NULL THEN 0 ELSE 1 END AS BIT) AS HasProofOfDelivery,
        t.CreatedDateUtc,
        t.RowVersion
    FROM core.Trip AS t
    INNER JOIN core.Booking        AS b   ON b.BookingId = t.BookingId
    INNER JOIN core.Customer       AS c   ON c.CustomerId = b.CustomerId
    INNER JOIN core.Vehicle        AS v   ON v.VehicleId = t.VehicleId
    INNER JOIN mst.VehicleType     AS vt  ON vt.VehicleTypeId = v.VehicleTypeId
    INNER JOIN core.Driver         AS d   ON d.DriverId = t.DriverId
    INNER JOIN core.VehicleOwner   AS o   ON o.OwnerId = t.OwnerId
    INNER JOIN core.BookingAddress AS pa  ON pa.BookingId = b.BookingId AND pa.AddressTypeId = 1
    INNER JOIN mst.City            AS pc  ON pc.CityId = pa.CityId
    INNER JOIN core.BookingAddress AS da  ON da.BookingId = b.BookingId AND da.AddressTypeId = 2
    INNER JOIN mst.City            AS dc  ON dc.CityId = da.CityId
    INNER JOIN core.BookingContact AS pct ON pct.BookingId = b.BookingId AND pct.ContactTypeId = 1
    INNER JOIN core.BookingContact AS dct ON dct.BookingId = b.BookingId AND dct.ContactTypeId = 2
    LEFT  JOIN core.ProofOfDelivery AS pod ON pod.TripId = t.TripId
    WHERE t.TripId = @TripId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_GetPaged
    @PageNumber     INT,
    @PageSize       INT,
    @Search         NVARCHAR(100) = NULL,
    @CustomerId     BIGINT        = NULL,
    @OwnerId        BIGINT        = NULL,
    @DriverId       BIGINT        = NULL,
    @TripStatusId   INT           = NULL,
    @ActiveOnly     BIT           = 0,
    @FromDateUtc    DATETIME2(3)  = NULL,
    @ToDateUtc      DATETIME2(3)  = NULL,
    @SortBy         VARCHAR(50)   = 'CreatedDate',
    @SortDirection  VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.TripId,
        t.TripNumber,
        t.BookingId,
        b.BookingNumber,
        c.FullName  AS CustomerName,
        v.VehicleNumber,
        d.FullName  AS DriverName,
        o.FullName  AS OwnerName,
        pc.Name     AS PickupCityName,
        dc.Name     AS DeliveryCityName,
        t.TripStatusId,
        t.PlannedPickupDateUtc,
        t.PlannedDeliveryDateUtc,
        t.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Trip AS t
    INNER JOIN core.Booking        AS b  ON b.BookingId = t.BookingId
    INNER JOIN core.Customer       AS c  ON c.CustomerId = b.CustomerId
    INNER JOIN core.Vehicle        AS v  ON v.VehicleId = t.VehicleId
    INNER JOIN core.Driver         AS d  ON d.DriverId = t.DriverId
    INNER JOIN core.VehicleOwner   AS o  ON o.OwnerId = t.OwnerId
    INNER JOIN core.BookingAddress AS pa ON pa.BookingId = b.BookingId AND pa.AddressTypeId = 1
    INNER JOIN mst.City            AS pc ON pc.CityId = pa.CityId
    INNER JOIN core.BookingAddress AS da ON da.BookingId = b.BookingId AND da.AddressTypeId = 2
    INNER JOIN mst.City            AS dc ON dc.CityId = da.CityId
    WHERE (@CustomerId IS NULL OR b.CustomerId = @CustomerId)
      AND (@OwnerId IS NULL OR t.OwnerId = @OwnerId)
      AND (@DriverId IS NULL OR t.DriverId = @DriverId)
      AND (@TripStatusId IS NULL OR t.TripStatusId = @TripStatusId)
      AND (@ActiveOnly = 0 OR t.TripStatusId IN (1, 2, 3, 9, 10))
      AND (@FromDateUtc IS NULL OR t.PlannedPickupDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR t.PlannedPickupDateUtc < @ToDateUtc)
      AND (@Search IS NULL
           OR t.TripNumber LIKE @Search + '%'
           OR b.BookingNumber LIKE @Search + '%'
           OR v.VehicleNumber LIKE N'%' + @Search + N'%'
           OR d.FullName LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'PickupDate'  AND @SortDirection = 'ASC'  THEN t.PlannedPickupDateUtc END ASC,
        CASE WHEN @SortBy = 'PickupDate'  AND @SortDirection = 'DESC' THEN t.PlannedPickupDateUtc END DESC,
        CASE WHEN @SortBy = 'TripNumber'  AND @SortDirection = 'ASC'  THEN t.TripNumber END ASC,
        CASE WHEN @SortBy = 'TripNumber'  AND @SortDirection = 'DESC' THEN t.TripNumber END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN t.CreatedDateUtc END ASC,
        t.CreatedDateUtc DESC,
        t.TripId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

/*
    Generic trip status change with all side effects in one transaction:
      PickupVerified -> ActualPickupDateUtc, StartOdometer
      Delivered      -> ActualDeliveryDateUtc, EndOdometer, vehicle + driver released
      Cancelled      -> CancellationReason, vehicle + driver released, assignments closed
      Exception      -> ExceptionReason
      OnHold         -> remembers the status to resume to
    @BookingNewStatusId (optional) moves the booking in the same transaction.
*/
CREATE OR ALTER PROCEDURE core.usp_Trip_UpdateStatus
    @TripId                  BIGINT,
    @ExpectedStatusId        INT,
    @NewStatusId             INT,
    @Remarks                 NVARCHAR(500),
    @ChangedBy               BIGINT,
    @Odometer                INT = NULL,
    @BookingExpectedStatusId INT = NULL,
    @BookingNewStatusId      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @VehicleId BIGINT, @DriverId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Trip
        SET @BookingId = BookingId, @VehicleId = VehicleId, @DriverId = DriverId,
            TripStatusId          = @NewStatusId,
            StatusBeforeHoldId    = CASE WHEN @NewStatusId = 9 THEN TripStatusId WHEN @ExpectedStatusId = 9 THEN NULL ELSE StatusBeforeHoldId END,
            ActualPickupDateUtc   = CASE WHEN @NewStatusId = 2 THEN @Now ELSE ActualPickupDateUtc END,
            StartOdometer         = CASE WHEN @NewStatusId = 2 AND @Odometer IS NOT NULL THEN @Odometer ELSE StartOdometer END,
            ActualDeliveryDateUtc = CASE WHEN @NewStatusId = 4 THEN @Now ELSE ActualDeliveryDateUtc END,
            EndOdometer           = CASE WHEN @NewStatusId = 4 AND @Odometer IS NOT NULL THEN @Odometer ELSE EndOdometer END,
            ExceptionReason       = CASE WHEN @NewStatusId = 10 THEN @Remarks ELSE ExceptionReason END,
            CancellationReason    = CASE WHEN @NewStatusId = 8 THEN @Remarks ELSE CancellationReason END,
            ModifiedBy            = @ChangedBy,
            ModifiedDateUtc       = @Now
        WHERE TripId = @TripId AND TripStatusId = @ExpectedStatusId;

        IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM core.Trip WHERE TripId = @TripId)
                THROW 50404, N'TRIP_NOT_FOUND|Trip was not found.', 1;
            THROW 50409, N'TRIP_STATUS_CHANGED|The trip status was changed by another request. Reload and try again.', 1;
        END

        INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@TripId, @ExpectedStatusId, @NewStatusId, @Remarks, @ChangedBy, @Now);

        IF @NewStatusId IN (4, 8)
        BEGIN
            UPDATE core.Vehicle SET IsAvailable = 1, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
            WHERE VehicleId = @VehicleId AND IsActive = 1 AND VerificationStatusId = 3;

            UPDATE core.Driver SET AvailabilityStatusId = 1, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
            WHERE DriverId = @DriverId AND AvailabilityStatusId = 2;

            INSERT INTO core.VehicleAvailabilityHistory (VehicleId, IsAvailable, Reason, ChangedBy, ChangedDateUtc)
            VALUES (@VehicleId, 1, N'Released from trip', @ChangedBy, @Now);

            INSERT INTO core.DriverAvailabilityHistory (DriverId, AvailabilityStatusId, Reason, ChangedBy, ChangedDateUtc)
            VALUES (@DriverId, 1, N'Released from trip', @ChangedBy, @Now);

            IF @NewStatusId = 8
                UPDATE core.TripAssignment SET ReleasedDateUtc = @Now WHERE TripId = @TripId AND ReleasedDateUtc IS NULL;
        END

        IF @BookingNewStatusId IS NOT NULL
        BEGIN
            UPDATE core.Booking
            SET BookingStatusId = @BookingNewStatusId, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
            WHERE BookingId = @BookingId AND BookingStatusId = @BookingExpectedStatusId;

            IF @@ROWCOUNT = 0
                THROW 50409, N'BOOKING_STATUS_CHANGED|The booking status was changed by another request. Reload and try again.', 1;

            INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@BookingId, @BookingExpectedStatusId, @BookingNewStatusId, @Remarks, @ChangedBy, @Now);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_GetStatusHistory
    @TripId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.TripStatusHistoryId AS HistoryId, h.FromStatusId, h.ToStatusId, h.Remarks, h.ChangedBy, u.FullName AS ChangedByName, h.ChangedDateUtc
    FROM core.TripStatusHistory AS h
    LEFT JOIN sec.[User] AS u ON u.UserId = h.ChangedBy
    WHERE h.TripId = @TripId
    ORDER BY h.ChangedDateUtc, h.TripStatusHistoryId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Trip_GetAssignments
    @TripId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ta.TripAssignmentId, ta.AssignmentTypeId, ta.VehicleId, v.VehicleNumber, ta.DriverId, d.FullName AS DriverName,
           ta.AssignedBy, u.FullName AS AssignedByName, ta.AssignedDateUtc, ta.ReleasedDateUtc, ta.Reason
    FROM core.TripAssignment AS ta
    LEFT JOIN core.Vehicle AS v ON v.VehicleId = ta.VehicleId
    LEFT JOIN core.Driver  AS d ON d.DriverId = ta.DriverId
    LEFT JOIN sec.[User]   AS u ON u.UserId = ta.AssignedBy
    WHERE ta.TripId = @TripId
    ORDER BY ta.AssignedDateUtc, ta.TripAssignmentId;
END
GO

-- ===== OTP verification =====

CREATE OR ALTER PROCEDURE core.usp_TripVerification_Create
    @TripId              BIGINT,
    @VerificationTypeId  INT,
    @OtpHash             BINARY(32),
    @SentToPhoneMasked   VARCHAR(20),
    @ExpiresDateUtc      DATETIME2(3),
    @MaxAttempts         INT,
    @MinResendSeconds    INT,
    @CreatedBy           BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM core.TripVerification
               WHERE TripId = @TripId AND VerificationTypeId = @VerificationTypeId AND IsSuperseded = 0 AND IsVerified = 0
                 AND CreatedDateUtc > DATEADD(SECOND, -@MinResendSeconds, SYSUTCDATETIME()))
        THROW 50400, N'OTP_RESEND_TOO_SOON|An OTP was sent recently. Please wait before requesting another.', 1;

    IF EXISTS (SELECT 1 FROM core.TripVerification
               WHERE TripId = @TripId AND VerificationTypeId = @VerificationTypeId AND IsVerified = 1)
        THROW 50400, N'ALREADY_VERIFIED|This step has already been verified.', 1;

    DECLARE @Id BIGINT;

    BEGIN TRANSACTION;
        UPDATE core.TripVerification SET IsSuperseded = 1
        WHERE TripId = @TripId AND VerificationTypeId = @VerificationTypeId AND IsSuperseded = 0;

        INSERT INTO core.TripVerification (TripId, VerificationTypeId, OtpHash, SentToPhoneMasked, ExpiresDateUtc, MaxAttempts, CreatedBy)
        VALUES (@TripId, @VerificationTypeId, @OtpHash, @SentToPhoneMasked, @ExpiresDateUtc, @MaxAttempts, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    COMMIT TRANSACTION;

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_TripVerification_GetActive
    @TripId              BIGINT,
    @VerificationTypeId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) tv.TripVerificationId, tv.OtpHash, tv.ExpiresDateUtc, tv.AttemptCount, tv.MaxAttempts, tv.IsVerified, tv.SentToPhoneMasked
    FROM core.TripVerification AS tv
    WHERE tv.TripId = @TripId AND tv.VerificationTypeId = @VerificationTypeId AND tv.IsSuperseded = 0
    ORDER BY tv.TripVerificationId DESC;
END
GO

-- Records one verification attempt atomically. Returns the attempt count after the call.
CREATE OR ALTER PROCEDURE core.usp_TripVerification_RegisterAttempt
    @TripVerificationId  BIGINT,
    @IsSuccess           BIT,
    @VerifiedBy          BIGINT,
    @ReceiverName        NVARCHAR(150),
    @Latitude            DECIMAL(9,6),
    @Longitude           DECIMAL(9,6)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Attempts INT;

    UPDATE core.TripVerification
    SET @Attempts = AttemptCount = AttemptCount + 1,
        IsVerified = @IsSuccess,
        VerifiedDateUtc = CASE WHEN @IsSuccess = 1 THEN SYSUTCDATETIME() ELSE NULL END,
        VerifiedBy = CASE WHEN @IsSuccess = 1 THEN @VerifiedBy ELSE NULL END,
        ReceiverName = CASE WHEN @IsSuccess = 1 THEN @ReceiverName ELSE NULL END,
        Latitude = CASE WHEN @IsSuccess = 1 THEN @Latitude ELSE NULL END,
        Longitude = CASE WHEN @IsSuccess = 1 THEN @Longitude ELSE NULL END
    WHERE TripVerificationId = @TripVerificationId
      AND IsVerified = 0
      AND IsSuperseded = 0
      AND AttemptCount < MaxAttempts
      AND ExpiresDateUtc > SYSUTCDATETIME();

    IF @@ROWCOUNT = 0
        THROW 50400, N'OTP_NOT_VERIFIABLE|The OTP has expired or the maximum number of attempts was reached. Request a new OTP.', 1;

    SELECT CAST(@Attempts AS BIGINT) AS Id;
END
GO

-- ===== Tracking =====

-- Points JSON: [{"latitude":12.97,"longitude":77.59,"recordedDateUtc":"2026-10-05T10:00:00Z","speedKmph":40,"heading":90,"accuracy":10}]
CREATE OR ALTER PROCEDURE core.usp_TripLocation_AddBatch
    @TripId              BIGINT,
    @TrackingProviderId  INT,
    @PointsJson          NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO core.TripLocationHistory (TripId, Latitude, Longitude, RecordedDateUtc, TrackingProviderId, Accuracy, SpeedKmph, Heading)
    SELECT @TripId, j.latitude, j.longitude, j.recordedDateUtc, @TrackingProviderId, j.accuracy, j.speedKmph, j.heading
    FROM OPENJSON(@PointsJson) WITH
    (
        latitude         DECIMAL(9,6) '$.latitude',
        longitude        DECIMAL(9,6) '$.longitude',
        recordedDateUtc  DATETIME2(3) '$.recordedDateUtc',
        accuracy         DECIMAL(8,2) '$.accuracy',
        speedKmph        DECIMAL(6,2) '$.speedKmph',
        heading          DECIMAL(5,2) '$.heading'
    ) AS j;

    SELECT CAST(@@ROWCOUNT AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_TripLocation_GetByTrip
    @TripId     BIGINT,
    @SinceUtc   DATETIME2(3) = NULL,
    @MaxPoints  INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT x.TripLocationHistoryId, x.Latitude, x.Longitude, x.RecordedDateUtc, x.SpeedKmph, x.Heading, x.Accuracy, x.TrackingProviderId
    FROM
    (
        SELECT TOP (@MaxPoints) l.TripLocationHistoryId, l.Latitude, l.Longitude, l.RecordedDateUtc, l.SpeedKmph, l.Heading, l.Accuracy, l.TrackingProviderId
        FROM core.TripLocationHistory AS l
        WHERE l.TripId = @TripId AND (@SinceUtc IS NULL OR l.RecordedDateUtc > @SinceUtc)
        ORDER BY l.RecordedDateUtc DESC
    ) AS x
    ORDER BY x.RecordedDateUtc;
END
GO

-- ===== Proof of delivery =====

-- Files JSON: [{"storedFileId":123,"fileCategory":"Photo"}]
CREATE OR ALTER PROCEDURE core.usp_ProofOfDelivery_Create
    @TripId            BIGINT,
    @ReceiverName      NVARCHAR(150),
    @ReceiverPhone     VARCHAR(20),
    @Remarks           NVARCHAR(1000),
    @Latitude          DECIMAL(9,6),
    @Longitude         DECIMAL(9,6),
    @SignatureFileId   BIGINT,
    @FilesJson         NVARCHAR(MAX),
    @UploadedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @PodId BIGINT, @DeliveredDateUtc DATETIME2(3);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @DeliveredDateUtc = ActualDeliveryDateUtc FROM core.Trip WITH (UPDLOCK) WHERE TripId = @TripId AND TripStatusId = 4;
        IF @DeliveredDateUtc IS NULL
            THROW 50400, N'TRIP_NOT_DELIVERED|Proof of delivery can only be uploaded after delivery is verified.', 1;

        INSERT INTO core.ProofOfDelivery (TripId, ReceiverName, ReceiverPhone, DeliveredDateUtc, Remarks, Latitude, Longitude, SignatureFileId, SignatureCapturedDateUtc, UploadedBy, CreatedDateUtc)
        VALUES (@TripId, @ReceiverName, @ReceiverPhone, @DeliveredDateUtc, @Remarks, @Latitude, @Longitude, @SignatureFileId,
                CASE WHEN @SignatureFileId IS NULL THEN NULL ELSE @Now END, @UploadedBy, @Now);
        SET @PodId = SCOPE_IDENTITY();

        INSERT INTO core.ProofOfDeliveryFile (ProofOfDeliveryId, StoredFileId, FileCategory, CreatedDateUtc)
        SELECT @PodId, j.storedFileId, j.fileCategory, @Now
        FROM OPENJSON(@FilesJson) WITH (storedFileId BIGINT '$.storedFileId', fileCategory VARCHAR(20) '$.fileCategory') AS j;

        UPDATE core.Trip SET TripStatusId = 5, ModifiedBy = @UploadedBy, ModifiedDateUtc = @Now WHERE TripId = @TripId;

        INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@TripId, 4, 5, N'Proof of delivery uploaded', @UploadedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @PodId AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_ProofOfDelivery_GetByTrip
    @TripId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pod.ProofOfDeliveryId, pod.TripId, pod.ReceiverName, pod.ReceiverPhone, pod.DeliveredDateUtc, pod.Remarks,
           pod.Latitude, pod.Longitude, pod.SignatureFileId, pod.UploadedBy, u.FullName AS UploadedByName, pod.CreatedDateUtc
    FROM core.ProofOfDelivery AS pod
    LEFT JOIN sec.[User] AS u ON u.UserId = pod.UploadedBy
    WHERE pod.TripId = @TripId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_ProofOfDeliveryFile_GetByTrip
    @TripId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pf.ProofOfDeliveryFileId, pf.StoredFileId, pf.FileCategory, sf.OriginalFileName, sf.ContentType, sf.SizeBytes, pf.CreatedDateUtc
    FROM core.ProofOfDelivery AS pod
    INNER JOIN core.ProofOfDeliveryFile AS pf ON pf.ProofOfDeliveryId = pod.ProofOfDeliveryId
    INNER JOIN core.StoredFile          AS sf ON sf.StoredFileId = pf.StoredFileId
    WHERE pod.TripId = @TripId
    ORDER BY pf.ProofOfDeliveryFileId;
END
GO
