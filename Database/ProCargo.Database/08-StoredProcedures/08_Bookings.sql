/*
    Module: Bookings (schema core)
    Status ids: 1 Draft, 2 Submitted, 3 UnderReview, 4 Quoted, 5 Confirmed, 6 Assigned, 7 InTransit,
                8 Delivered, 9 Invoiced, 10 Paid, 11 Closed, 12 Cancelled, 13 OnHold, 14 Rejected

    Which transitions are allowed is decided by ProCargo.Domain (BookingStatusRules) in the API.
    The procedures add the race-safety net: every status change states the status it expects to move FROM
    (@ExpectedStatusId); if another request changed the booking first, 0 rows match and we raise a conflict.

    Items are passed as JSON: [{"description":"..","quantity":1,"weightKg":10.5,"lengthCm":null,
                                "widthCm":null,"heightCm":null,"isFragile":false,"declaredValue":null}]
*/

CREATE OR ALTER PROCEDURE core.usp_Booking_Create
    @CustomerId              BIGINT,
    @VehicleTypeId           INT,
    @GoodsTypeId             INT,
    @GoodsDescription        NVARCHAR(500),
    @RequestedPickupDateUtc  DATETIME2(3),
    @SpecialInstructions     NVARCHAR(1000),
    @EstimatedDistanceKm     DECIMAL(8,2),
    @Submit                  BIT,
    @PickupAddressLine1      NVARCHAR(200),
    @PickupAddressLine2      NVARCHAR(200),
    @PickupLandmark          NVARCHAR(150),
    @PickupCityId            INT,
    @PickupPincode           CHAR(6),
    @PickupLatitude          DECIMAL(9,6),
    @PickupLongitude         DECIMAL(9,6),
    @DeliveryAddressLine1    NVARCHAR(200),
    @DeliveryAddressLine2    NVARCHAR(200),
    @DeliveryLandmark        NVARCHAR(150),
    @DeliveryCityId          INT,
    @DeliveryPincode         CHAR(6),
    @DeliveryLatitude        DECIMAL(9,6),
    @DeliveryLongitude       DECIMAL(9,6),
    @PickupContactName       NVARCHAR(150),
    @PickupContactPhone      VARCHAR(20),
    @PickupContactAltPhone   VARCHAR(20),
    @DeliveryContactName     NVARCHAR(150),
    @DeliveryContactPhone    VARCHAR(20),
    @DeliveryContactAltPhone VARCHAR(20),
    @ItemsJson               NVARCHAR(MAX),
    @CreatedBy               BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM core.Customer WHERE CustomerId = @CustomerId AND IsActive = 1 AND IsDeleted = 0)
        THROW 50400, N'CUSTOMER_INACTIVE|Bookings can only be created for an active customer.', 1;

    DECLARE @Items TABLE
    (
        Description   NVARCHAR(200) NOT NULL,
        Quantity      INT           NOT NULL,
        WeightKg      DECIMAL(10,2) NOT NULL,
        LengthCm      DECIMAL(8,2)  NULL,
        WidthCm       DECIMAL(8,2)  NULL,
        HeightCm      DECIMAL(8,2)  NULL,
        IsFragile     BIT           NOT NULL,
        DeclaredValue DECIMAL(14,2) NULL
    );

    INSERT INTO @Items (Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue)
    SELECT j.description, j.quantity, j.weightKg, j.lengthCm, j.widthCm, j.heightCm, ISNULL(j.isFragile, 0), j.declaredValue
    FROM OPENJSON(@ItemsJson) WITH
    (
        description   NVARCHAR(200) '$.description',
        quantity      INT           '$.quantity',
        weightKg      DECIMAL(10,2) '$.weightKg',
        lengthCm      DECIMAL(8,2)  '$.lengthCm',
        widthCm       DECIMAL(8,2)  '$.widthCm',
        heightCm      DECIMAL(8,2)  '$.heightCm',
        isFragile     BIT           '$.isFragile',
        declaredValue DECIMAL(14,2) '$.declaredValue'
    ) AS j;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50400, N'BOOKING_ITEMS_REQUIRED|At least one item is required.', 1;

    DECLARE @TotalWeight DECIMAL(10,2) = (SELECT SUM(WeightKg) FROM @Items);
    DECLARE @TotalQuantity INT = (SELECT SUM(Quantity) FROM @Items);
    DECLARE @StatusId INT = CASE WHEN @Submit = 1 THEN 2 ELSE 1 END;
    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @BookingNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        SET @BookingNumber = core.fn_FormatBusinessNumber('BKG', NEXT VALUE FOR core.seq_BookingNumber, @Now);

        INSERT INTO core.Booking
            (BookingNumber, CustomerId, VehicleTypeId, GoodsTypeId, GoodsDescription, TotalWeightKg, TotalQuantity,
             RequestedPickupDateUtc, SpecialInstructions, EstimatedDistanceKm, BookingStatusId, CreatedBy, CreatedDateUtc)
        VALUES
            (@BookingNumber, @CustomerId, @VehicleTypeId, @GoodsTypeId, @GoodsDescription, @TotalWeight, @TotalQuantity,
             @RequestedPickupDateUtc, @SpecialInstructions, @EstimatedDistanceKm, @StatusId, @CreatedBy, @Now);
        SET @BookingId = SCOPE_IDENTITY();

        INSERT INTO core.BookingAddress (BookingId, AddressTypeId, AddressLine1, AddressLine2, Landmark, CityId, Pincode, Latitude, Longitude)
        VALUES (@BookingId, 1, @PickupAddressLine1, @PickupAddressLine2, @PickupLandmark, @PickupCityId, @PickupPincode, @PickupLatitude, @PickupLongitude),
               (@BookingId, 2, @DeliveryAddressLine1, @DeliveryAddressLine2, @DeliveryLandmark, @DeliveryCityId, @DeliveryPincode, @DeliveryLatitude, @DeliveryLongitude);

        INSERT INTO core.BookingContact (BookingId, ContactTypeId, ContactName, PhoneNumber, AlternatePhoneNumber)
        VALUES (@BookingId, 1, @PickupContactName, @PickupContactPhone, @PickupContactAltPhone),
               (@BookingId, 2, @DeliveryContactName, @DeliveryContactPhone, @DeliveryContactAltPhone);

        INSERT INTO core.BookingItem (BookingId, Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue)
        SELECT @BookingId, Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue FROM @Items;

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, NULL, @StatusId, N'Booking created', @CreatedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @BookingId AS Id, @BookingNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Booking_Update
    @BookingId               BIGINT,
    @VehicleTypeId           INT,
    @GoodsTypeId             INT,
    @GoodsDescription        NVARCHAR(500),
    @RequestedPickupDateUtc  DATETIME2(3),
    @SpecialInstructions     NVARCHAR(1000),
    @EstimatedDistanceKm     DECIMAL(8,2),
    @PickupAddressLine1      NVARCHAR(200),
    @PickupAddressLine2      NVARCHAR(200),
    @PickupLandmark          NVARCHAR(150),
    @PickupCityId            INT,
    @PickupPincode           CHAR(6),
    @PickupLatitude          DECIMAL(9,6),
    @PickupLongitude         DECIMAL(9,6),
    @DeliveryAddressLine1    NVARCHAR(200),
    @DeliveryAddressLine2    NVARCHAR(200),
    @DeliveryLandmark        NVARCHAR(150),
    @DeliveryCityId          INT,
    @DeliveryPincode         CHAR(6),
    @DeliveryLatitude        DECIMAL(9,6),
    @DeliveryLongitude       DECIMAL(9,6),
    @PickupContactName       NVARCHAR(150),
    @PickupContactPhone      VARCHAR(20),
    @PickupContactAltPhone   VARCHAR(20),
    @DeliveryContactName     NVARCHAR(150),
    @DeliveryContactPhone    VARCHAR(20),
    @DeliveryContactAltPhone VARCHAR(20),
    @ItemsJson               NVARCHAR(MAX),
    @ModifiedBy              BIGINT,
    @RowVersion              BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Items TABLE
    (
        Description   NVARCHAR(200) NOT NULL,
        Quantity      INT           NOT NULL,
        WeightKg      DECIMAL(10,2) NOT NULL,
        LengthCm      DECIMAL(8,2)  NULL,
        WidthCm       DECIMAL(8,2)  NULL,
        HeightCm      DECIMAL(8,2)  NULL,
        IsFragile     BIT           NOT NULL,
        DeclaredValue DECIMAL(14,2) NULL
    );

    INSERT INTO @Items (Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue)
    SELECT j.description, j.quantity, j.weightKg, j.lengthCm, j.widthCm, j.heightCm, ISNULL(j.isFragile, 0), j.declaredValue
    FROM OPENJSON(@ItemsJson) WITH
    (
        description   NVARCHAR(200) '$.description',
        quantity      INT           '$.quantity',
        weightKg      DECIMAL(10,2) '$.weightKg',
        lengthCm      DECIMAL(8,2)  '$.lengthCm',
        widthCm       DECIMAL(8,2)  '$.widthCm',
        heightCm      DECIMAL(8,2)  '$.heightCm',
        isFragile     BIT           '$.isFragile',
        declaredValue DECIMAL(14,2) '$.declaredValue'
    ) AS j;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50400, N'BOOKING_ITEMS_REQUIRED|At least one item is required.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Booking
        SET VehicleTypeId = @VehicleTypeId,
            GoodsTypeId = @GoodsTypeId,
            GoodsDescription = @GoodsDescription,
            TotalWeightKg = (SELECT SUM(WeightKg) FROM @Items),
            TotalQuantity = (SELECT SUM(Quantity) FROM @Items),
            RequestedPickupDateUtc = @RequestedPickupDateUtc,
            SpecialInstructions = @SpecialInstructions,
            EstimatedDistanceKm = @EstimatedDistanceKm,
            ModifiedBy = @ModifiedBy,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE BookingId = @BookingId AND RowVersion = @RowVersion AND BookingStatusId IN (1, 2);

        IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM core.Booking WHERE BookingId = @BookingId)
                THROW 50404, N'BOOKING_NOT_FOUND|Booking was not found.', 1;
            IF NOT EXISTS (SELECT 1 FROM core.Booking WHERE BookingId = @BookingId AND BookingStatusId IN (1, 2))
                THROW 50400, N'BOOKING_NOT_EDITABLE|Only draft or submitted bookings can be edited.', 1;
            THROW 50412, N'CONCURRENCY_CONFLICT|The booking was modified by someone else. Reload and try again.', 1;
        END

        UPDATE core.BookingAddress
        SET AddressLine1 = @PickupAddressLine1, AddressLine2 = @PickupAddressLine2, Landmark = @PickupLandmark, CityId = @PickupCityId,
            Pincode = @PickupPincode, Latitude = @PickupLatitude, Longitude = @PickupLongitude
        WHERE BookingId = @BookingId AND AddressTypeId = 1;

        UPDATE core.BookingAddress
        SET AddressLine1 = @DeliveryAddressLine1, AddressLine2 = @DeliveryAddressLine2, Landmark = @DeliveryLandmark, CityId = @DeliveryCityId,
            Pincode = @DeliveryPincode, Latitude = @DeliveryLatitude, Longitude = @DeliveryLongitude
        WHERE BookingId = @BookingId AND AddressTypeId = 2;

        UPDATE core.BookingContact
        SET ContactName = @PickupContactName, PhoneNumber = @PickupContactPhone, AlternatePhoneNumber = @PickupContactAltPhone
        WHERE BookingId = @BookingId AND ContactTypeId = 1;

        UPDATE core.BookingContact
        SET ContactName = @DeliveryContactName, PhoneNumber = @DeliveryContactPhone, AlternatePhoneNumber = @DeliveryContactAltPhone
        WHERE BookingId = @BookingId AND ContactTypeId = 2;

        DELETE FROM core.BookingItem WHERE BookingId = @BookingId;

        INSERT INTO core.BookingItem (BookingId, Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue)
        SELECT @BookingId, Description, Quantity, WeightKg, LengthCm, WidthCm, HeightCm, IsFragile, DeclaredValue FROM @Items;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Booking_GetById
    @BookingId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        b.BookingId,
        b.BookingNumber,
        b.CustomerId,
        c.FullName              AS CustomerName,
        c.UserId                AS CustomerUserId,
        c.CompanyName           AS CustomerCompanyName,
        c.PhoneNumber           AS CustomerPhoneNumber,
        b.VehicleTypeId,
        vt.Name                 AS VehicleTypeName,
        b.GoodsTypeId,
        gt.Name                 AS GoodsTypeName,
        gt.RequiresSpecialHandling,
        b.GoodsDescription,
        b.TotalWeightKg,
        b.TotalQuantity,
        b.RequestedPickupDateUtc,
        b.SpecialInstructions,
        b.EstimatedDistanceKm,
        b.BookingStatusId,
        b.StatusBeforeHoldId,
        b.CancellationReason,
        pa.AddressLine1         AS PickupAddressLine1,
        pa.AddressLine2         AS PickupAddressLine2,
        pa.Landmark             AS PickupLandmark,
        pa.CityId               AS PickupCityId,
        pc.Name                 AS PickupCityName,
        pc.StateId              AS PickupStateId,
        pa.Pincode              AS PickupPincode,
        pa.Latitude             AS PickupLatitude,
        pa.Longitude            AS PickupLongitude,
        da.AddressLine1         AS DeliveryAddressLine1,
        da.AddressLine2         AS DeliveryAddressLine2,
        da.Landmark             AS DeliveryLandmark,
        da.CityId               AS DeliveryCityId,
        dc.Name                 AS DeliveryCityName,
        dc.StateId              AS DeliveryStateId,
        da.Pincode              AS DeliveryPincode,
        da.Latitude             AS DeliveryLatitude,
        da.Longitude            AS DeliveryLongitude,
        pct.ContactName         AS PickupContactName,
        pct.PhoneNumber         AS PickupContactPhone,
        pct.AlternatePhoneNumber AS PickupContactAltPhone,
        dct.ContactName         AS DeliveryContactName,
        dct.PhoneNumber         AS DeliveryContactPhone,
        dct.AlternatePhoneNumber AS DeliveryContactAltPhone,
        q.QuotationId           AS AcceptedQuotationId,
        q.TotalAmount           AS AcceptedQuotationAmount,
        t.TripId,
        t.TripNumber,
        t.TripStatusId,
        t.OwnerId               AS AssignedOwnerId,
        t.DriverId              AS AssignedDriverId,
        v.VehicleNumber         AS AssignedVehicleNumber,
        d.FullName              AS AssignedDriverName,
        d.PhoneNumber           AS AssignedDriverPhone,
        inv.InvoiceId,
        b.CreatedDateUtc,
        b.ModifiedDateUtc,
        b.RowVersion
    FROM core.Booking AS b
    INNER JOIN core.Customer        AS c   ON c.CustomerId = b.CustomerId
    INNER JOIN mst.VehicleType      AS vt  ON vt.VehicleTypeId = b.VehicleTypeId
    INNER JOIN mst.GoodsType        AS gt  ON gt.GoodsTypeId = b.GoodsTypeId
    INNER JOIN core.BookingAddress  AS pa  ON pa.BookingId = b.BookingId AND pa.AddressTypeId = 1
    INNER JOIN mst.City             AS pc  ON pc.CityId = pa.CityId
    INNER JOIN core.BookingAddress  AS da  ON da.BookingId = b.BookingId AND da.AddressTypeId = 2
    INNER JOIN mst.City             AS dc  ON dc.CityId = da.CityId
    INNER JOIN core.BookingContact  AS pct ON pct.BookingId = b.BookingId AND pct.ContactTypeId = 1
    INNER JOIN core.BookingContact  AS dct ON dct.BookingId = b.BookingId AND dct.ContactTypeId = 2
    LEFT  JOIN core.Quotation       AS q   ON q.BookingId = b.BookingId AND q.QuotationStatusId = 3
    LEFT  JOIN core.Trip            AS t   ON t.BookingId = b.BookingId AND t.TripStatusId <> 8
    LEFT  JOIN core.Vehicle         AS v   ON v.VehicleId = t.VehicleId
    LEFT  JOIN core.Driver          AS d   ON d.DriverId = t.DriverId
    LEFT  JOIN fin.Invoice          AS inv ON inv.BookingId = b.BookingId AND inv.InvoiceStatusId <> 5
    WHERE b.BookingId = @BookingId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_BookingItem_GetByBooking
    @BookingId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT bi.BookingItemId, bi.Description, bi.Quantity, bi.WeightKg, bi.LengthCm, bi.WidthCm, bi.HeightCm, bi.IsFragile, bi.DeclaredValue
    FROM core.BookingItem AS bi
    WHERE bi.BookingId = @BookingId
    ORDER BY bi.BookingItemId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Booking_GetPaged
    @PageNumber       INT,
    @PageSize         INT,
    @Search           NVARCHAR(100) = NULL,
    @CustomerId       BIGINT        = NULL,
    @OwnerId          BIGINT        = NULL,
    @DriverId         BIGINT        = NULL,
    @BookingStatusId  INT           = NULL,
    @VehicleTypeId    INT           = NULL,
    @FromDateUtc      DATETIME2(3)  = NULL,
    @ToDateUtc        DATETIME2(3)  = NULL,
    @SortBy           VARCHAR(50)   = 'CreatedDate',
    @SortDirection    VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        bo.BookingId,
        bo.BookingNumber,
        bo.CustomerId,
        bo.CustomerName,
        bo.VehicleTypeId,
        bo.VehicleTypeName,
        bo.TotalWeightKg,
        bo.PickupCityName,
        bo.DeliveryCityName,
        bo.RequestedPickupDateUtc,
        bo.BookingStatusId,
        bo.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM rpt.vw_BookingOverview AS bo
    WHERE (@CustomerId IS NULL OR bo.CustomerId = @CustomerId)
      AND (@BookingStatusId IS NULL OR bo.BookingStatusId = @BookingStatusId)
      AND (@VehicleTypeId IS NULL OR bo.VehicleTypeId = @VehicleTypeId)
      AND (@FromDateUtc IS NULL OR bo.CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR bo.CreatedDateUtc < @ToDateUtc)
      AND (@OwnerId IS NULL OR EXISTS (SELECT 1 FROM core.Trip AS t WHERE t.BookingId = bo.BookingId AND t.OwnerId = @OwnerId AND t.TripStatusId <> 8))
      AND (@DriverId IS NULL OR EXISTS (SELECT 1 FROM core.Trip AS t WHERE t.BookingId = bo.BookingId AND t.DriverId = @DriverId AND t.TripStatusId <> 8))
      AND (@Search IS NULL
           OR bo.BookingNumber LIKE @Search + '%'
           OR bo.CustomerName LIKE N'%' + @Search + N'%'
           OR bo.PickupCityName LIKE @Search + N'%'
           OR bo.DeliveryCityName LIKE @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'BookingNumber' AND @SortDirection = 'ASC'  THEN bo.BookingNumber END ASC,
        CASE WHEN @SortBy = 'BookingNumber' AND @SortDirection = 'DESC' THEN bo.BookingNumber END DESC,
        CASE WHEN @SortBy = 'PickupDate'    AND @SortDirection = 'ASC'  THEN bo.RequestedPickupDateUtc END ASC,
        CASE WHEN @SortBy = 'PickupDate'    AND @SortDirection = 'DESC' THEN bo.RequestedPickupDateUtc END DESC,
        CASE WHEN @SortBy = 'CreatedDate'   AND @SortDirection = 'ASC'  THEN bo.CreatedDateUtc END ASC,
        bo.CreatedDateUtc DESC,
        bo.BookingId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_Booking_ChangeStatus
    @BookingId         BIGINT,
    @ExpectedStatusId  INT,
    @NewStatusId       INT,
    @Remarks           NVARCHAR(500),
    @ChangedBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Booking
        SET BookingStatusId = @NewStatusId,
            StatusBeforeHoldId = CASE WHEN @NewStatusId = 13 THEN BookingStatusId
                                      WHEN @ExpectedStatusId = 13 THEN NULL
                                      ELSE StatusBeforeHoldId END,
            ModifiedBy = @ChangedBy,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE BookingId = @BookingId AND BookingStatusId = @ExpectedStatusId;

        IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM core.Booking WHERE BookingId = @BookingId)
                THROW 50404, N'BOOKING_NOT_FOUND|Booking was not found.', 1;
            THROW 50409, N'BOOKING_STATUS_CHANGED|The booking status was changed by another request. Reload and try again.', 1;
        END

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy)
        VALUES (@BookingId, @ExpectedStatusId, @NewStatusId, @Remarks, @ChangedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Cancels the booking and everything still open underneath it (quotations, a scheduled trip).
CREATE OR ALTER PROCEDURE core.usp_Booking_Cancel
    @BookingId         BIGINT,
    @ExpectedStatusId  INT,
    @Reason            NVARCHAR(500),
    @ChangedBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Booking
        SET BookingStatusId = 12, CancellationReason = @Reason, StatusBeforeHoldId = NULL,
            ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
        WHERE BookingId = @BookingId AND BookingStatusId = @ExpectedStatusId;

        IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM core.Booking WHERE BookingId = @BookingId)
                THROW 50404, N'BOOKING_NOT_FOUND|Booking was not found.', 1;
            THROW 50409, N'BOOKING_STATUS_CHANGED|The booking status was changed by another request. Reload and try again.', 1;
        END

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, @ExpectedStatusId, 12, @Reason, @ChangedBy, @Now);

        -- withdraw open quotations
        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        SELECT q.QuotationId, q.QuotationStatusId, 6, N'Booking cancelled', @ChangedBy, @Now
        FROM core.Quotation AS q WHERE q.BookingId = @BookingId AND q.QuotationStatusId IN (1, 2);

        UPDATE core.Quotation SET QuotationStatusId = 6, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
        WHERE BookingId = @BookingId AND QuotationStatusId IN (1, 2);

        -- cancel a trip that has not started yet and release its vehicle and driver
        DECLARE @TripId BIGINT, @VehicleId BIGINT, @DriverId BIGINT, @TripStatusId INT;
        SELECT @TripId = TripId, @VehicleId = VehicleId, @DriverId = DriverId, @TripStatusId = TripStatusId
        FROM core.Trip WHERE BookingId = @BookingId AND TripStatusId IN (1, 9);

        IF @TripId IS NOT NULL
        BEGIN
            UPDATE core.Trip SET TripStatusId = 8, CancellationReason = @Reason, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
            WHERE TripId = @TripId;

            INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@TripId, @TripStatusId, 8, N'Booking cancelled', @ChangedBy, @Now);

            UPDATE core.TripAssignment SET ReleasedDateUtc = @Now WHERE TripId = @TripId AND ReleasedDateUtc IS NULL;
            UPDATE core.Vehicle SET IsAvailable = 1, ModifiedDateUtc = @Now WHERE VehicleId = @VehicleId AND IsActive = 1 AND VerificationStatusId = 3;
            UPDATE core.Driver  SET AvailabilityStatusId = 1, ModifiedDateUtc = @Now WHERE DriverId = @DriverId AND AvailabilityStatusId = 2;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Booking_GetStatusHistory
    @BookingId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.BookingStatusHistoryId AS HistoryId, h.FromStatusId, h.ToStatusId, h.Remarks, h.ChangedBy, u.FullName AS ChangedByName, h.ChangedDateUtc
    FROM core.BookingStatusHistory AS h
    LEFT JOIN sec.[User] AS u ON u.UserId = h.ChangedBy
    WHERE h.BookingId = @BookingId
    ORDER BY h.ChangedDateUtc, h.BookingStatusHistoryId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_BookingNote_Create
    @BookingId   BIGINT,
    @Note        NVARCHAR(1000),
    @IsInternal  BIT,
    @CreatedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO core.BookingNote (BookingId, Note, IsInternal, CreatedBy) VALUES (@BookingId, @Note, @IsInternal, @CreatedBy);
    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_BookingNote_GetByBooking
    @BookingId        BIGINT,
    @IncludeInternal  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.BookingNoteId, n.Note, n.IsInternal, n.CreatedBy, u.FullName AS CreatedByName, n.CreatedDateUtc
    FROM core.BookingNote AS n
    LEFT JOIN sec.[User] AS u ON u.UserId = n.CreatedBy
    WHERE n.BookingId = @BookingId AND (@IncludeInternal = 1 OR n.IsInternal = 0)
    ORDER BY n.CreatedDateUtc DESC;
END
GO
