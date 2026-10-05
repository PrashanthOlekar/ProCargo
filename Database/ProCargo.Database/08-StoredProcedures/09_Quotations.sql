/*
    Module: Quotations (schema core)
    Quotation status: 1 Draft, 2 Sent, 3 Accepted, 4 Rejected, 5 Expired, 6 Withdrawn
    Amounts are calculated by the API PricingService (configurable pricing tables) and persisted here
    together with the charge breakdown (JSON):
      [{"chargeCode":"BASE","description":"Base fare","quantity":1,"unitRate":800,"amount":800,"sortOrder":1}]
*/

CREATE OR ALTER PROCEDURE core.usp_Quotation_Create
    @BookingId              BIGINT,
    @DistanceKm             DECIMAL(8,2),
    @BaseAmount             DECIMAL(14,2),
    @DistanceCharge         DECIMAL(14,2),
    @LoadingCharge          DECIMAL(14,2),
    @UnloadingCharge        DECIMAL(14,2),
    @WaitingCharge          DECIMAL(14,2),
    @TollCharge             DECIMAL(14,2),
    @NightCharge            DECIMAL(14,2),
    @SpecialHandlingCharge  DECIMAL(14,2),
    @AdjustmentAmount       DECIMAL(14,2),
    @DiscountAmount         DECIMAL(14,2),
    @SubTotal               DECIMAL(14,2),
    @TaxPercent             DECIMAL(5,2),
    @TaxAmount              DECIMAL(14,2),
    @TotalAmount            DECIMAL(14,2),
    @ValidityDateUtc        DATETIME2(3),
    @Notes                  NVARCHAR(1000),
    @ChargesJson            NVARCHAR(MAX),
    @CreatedBy              BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @QuotationId BIGINT, @QuotationNumber VARCHAR(30), @BookingStatusId INT, @VersionNo INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @BookingStatusId = BookingStatusId FROM core.Booking WITH (UPDLOCK) WHERE BookingId = @BookingId;

        IF @BookingStatusId IS NULL
            THROW 50404, N'BOOKING_NOT_FOUND|Booking was not found.', 1;

        IF @BookingStatusId NOT IN (2, 3, 4)
            THROW 50400, N'BOOKING_NOT_QUOTABLE|Quotations can only be prepared for submitted bookings under review.', 1;

        SET @VersionNo = ISNULL((SELECT MAX(VersionNo) FROM core.Quotation WHERE BookingId = @BookingId), 0) + 1;
        SET @QuotationNumber = core.fn_FormatBusinessNumber('QUO', NEXT VALUE FOR core.seq_QuotationNumber, @Now);

        INSERT INTO core.Quotation
            (QuotationNumber, BookingId, VersionNo, DistanceKm, BaseAmount, DistanceCharge, LoadingCharge, UnloadingCharge, WaitingCharge,
             TollCharge, NightCharge, SpecialHandlingCharge, AdjustmentAmount, DiscountAmount, SubTotal, TaxPercent, TaxAmount, TotalAmount,
             ValidityDateUtc, QuotationStatusId, Notes, CreatedBy, CreatedDateUtc)
        VALUES
            (@QuotationNumber, @BookingId, @VersionNo, @DistanceKm, @BaseAmount, @DistanceCharge, @LoadingCharge, @UnloadingCharge, @WaitingCharge,
             @TollCharge, @NightCharge, @SpecialHandlingCharge, @AdjustmentAmount, @DiscountAmount, @SubTotal, @TaxPercent, @TaxAmount, @TotalAmount,
             @ValidityDateUtc, 1, @Notes, @CreatedBy, @Now);
        SET @QuotationId = SCOPE_IDENTITY();

        INSERT INTO core.QuotationCharge (QuotationId, ChargeCode, Description, Quantity, UnitRate, Amount, SortOrder)
        SELECT @QuotationId, j.chargeCode, j.description, j.quantity, j.unitRate, j.amount, j.sortOrder
        FROM OPENJSON(@ChargesJson) WITH
        (
            chargeCode  VARCHAR(30)   '$.chargeCode',
            description NVARCHAR(200) '$.description',
            quantity    DECIMAL(10,2) '$.quantity',
            unitRate    DECIMAL(12,2) '$.unitRate',
            amount      DECIMAL(14,2) '$.amount',
            sortOrder   INT           '$.sortOrder'
        ) AS j;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@QuotationId, NULL, 1, N'Quotation drafted', @CreatedBy, @Now);

        IF @BookingStatusId = 2
        BEGIN
            UPDATE core.Booking SET BookingStatusId = 3, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;
            INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@BookingId, 2, 3, N'Under review - quotation being prepared', @CreatedBy, @Now);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @QuotationId AS Id, @QuotationNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Quotation_Update
    @QuotationId            BIGINT,
    @DistanceKm             DECIMAL(8,2),
    @BaseAmount             DECIMAL(14,2),
    @DistanceCharge         DECIMAL(14,2),
    @LoadingCharge          DECIMAL(14,2),
    @UnloadingCharge        DECIMAL(14,2),
    @WaitingCharge          DECIMAL(14,2),
    @TollCharge             DECIMAL(14,2),
    @NightCharge            DECIMAL(14,2),
    @SpecialHandlingCharge  DECIMAL(14,2),
    @AdjustmentAmount       DECIMAL(14,2),
    @DiscountAmount         DECIMAL(14,2),
    @SubTotal               DECIMAL(14,2),
    @TaxPercent             DECIMAL(5,2),
    @TaxAmount              DECIMAL(14,2),
    @TotalAmount            DECIMAL(14,2),
    @ValidityDateUtc        DATETIME2(3),
    @Notes                  NVARCHAR(1000),
    @ChargesJson            NVARCHAR(MAX),
    @ModifiedBy             BIGINT,
    @RowVersion             BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Quotation
        SET DistanceKm = @DistanceKm, BaseAmount = @BaseAmount, DistanceCharge = @DistanceCharge, LoadingCharge = @LoadingCharge,
            UnloadingCharge = @UnloadingCharge, WaitingCharge = @WaitingCharge, TollCharge = @TollCharge, NightCharge = @NightCharge,
            SpecialHandlingCharge = @SpecialHandlingCharge, AdjustmentAmount = @AdjustmentAmount, DiscountAmount = @DiscountAmount,
            SubTotal = @SubTotal, TaxPercent = @TaxPercent, TaxAmount = @TaxAmount, TotalAmount = @TotalAmount,
            ValidityDateUtc = @ValidityDateUtc, Notes = @Notes, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE QuotationId = @QuotationId AND QuotationStatusId = 1 AND RowVersion = @RowVersion;

        IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM core.Quotation WHERE QuotationId = @QuotationId)
                THROW 50404, N'QUOTATION_NOT_FOUND|Quotation was not found.', 1;
            IF NOT EXISTS (SELECT 1 FROM core.Quotation WHERE QuotationId = @QuotationId AND QuotationStatusId = 1)
                THROW 50400, N'QUOTATION_NOT_EDITABLE|Only draft quotations can be edited.', 1;
            THROW 50412, N'CONCURRENCY_CONFLICT|The quotation was modified by someone else. Reload and try again.', 1;
        END

        DELETE FROM core.QuotationCharge WHERE QuotationId = @QuotationId;

        INSERT INTO core.QuotationCharge (QuotationId, ChargeCode, Description, Quantity, UnitRate, Amount, SortOrder)
        SELECT @QuotationId, j.chargeCode, j.description, j.quantity, j.unitRate, j.amount, j.sortOrder
        FROM OPENJSON(@ChargesJson) WITH
        (
            chargeCode  VARCHAR(30)   '$.chargeCode',
            description NVARCHAR(200) '$.description',
            quantity    DECIMAL(10,2) '$.quantity',
            unitRate    DECIMAL(12,2) '$.unitRate',
            amount      DECIMAL(14,2) '$.amount',
            sortOrder   INT           '$.sortOrder'
        ) AS j;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Quotation_GetById
    @QuotationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        q.QuotationId,
        q.QuotationNumber,
        q.BookingId,
        b.BookingNumber,
        b.CustomerId,
        c.FullName AS CustomerName,
        c.UserId AS CustomerUserId,
        b.BookingStatusId,
        q.VersionNo,
        q.DistanceKm,
        q.BaseAmount,
        q.DistanceCharge,
        q.LoadingCharge,
        q.UnloadingCharge,
        q.WaitingCharge,
        q.TollCharge,
        q.NightCharge,
        q.SpecialHandlingCharge,
        q.AdjustmentAmount,
        q.DiscountAmount,
        q.SubTotal,
        q.TaxPercent,
        q.TaxAmount,
        q.TotalAmount,
        q.ValidityDateUtc,
        q.QuotationStatusId,
        q.Notes,
        q.RejectionReason,
        q.SentDateUtc,
        q.RespondedDateUtc,
        q.CreatedDateUtc,
        q.RowVersion
    FROM core.Quotation AS q
    INNER JOIN core.Booking  AS b ON b.BookingId = q.BookingId
    INNER JOIN core.Customer AS c ON c.CustomerId = b.CustomerId
    WHERE q.QuotationId = @QuotationId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_QuotationCharge_GetByQuotation
    @QuotationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT qc.QuotationChargeId, qc.ChargeCode, qc.Description, qc.Quantity, qc.UnitRate, qc.Amount, qc.SortOrder
    FROM core.QuotationCharge AS qc
    WHERE qc.QuotationId = @QuotationId
    ORDER BY qc.SortOrder, qc.QuotationChargeId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Quotation_GetPaged
    @PageNumber         INT,
    @PageSize           INT,
    @Search             NVARCHAR(100) = NULL,
    @CustomerId         BIGINT        = NULL,
    @BookingId          BIGINT        = NULL,
    @QuotationStatusId  INT           = NULL,
    @ExcludeDrafts      BIT           = 0,
    @SortBy             VARCHAR(50)   = 'CreatedDate',
    @SortDirection      VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        q.QuotationId,
        q.QuotationNumber,
        q.BookingId,
        b.BookingNumber,
        b.CustomerId,
        c.FullName AS CustomerName,
        q.VersionNo,
        q.TotalAmount,
        q.ValidityDateUtc,
        q.QuotationStatusId,
        q.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Quotation AS q
    INNER JOIN core.Booking  AS b ON b.BookingId = q.BookingId
    INNER JOIN core.Customer AS c ON c.CustomerId = b.CustomerId
    WHERE (@CustomerId IS NULL OR b.CustomerId = @CustomerId)
      AND (@BookingId IS NULL OR q.BookingId = @BookingId)
      AND (@QuotationStatusId IS NULL OR q.QuotationStatusId = @QuotationStatusId)
      AND (@ExcludeDrafts = 0 OR q.QuotationStatusId <> 1)
      AND (@Search IS NULL OR q.QuotationNumber LIKE @Search + '%' OR b.BookingNumber LIKE @Search + '%' OR c.FullName LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'Total'       AND @SortDirection = 'ASC'  THEN q.TotalAmount END ASC,
        CASE WHEN @SortBy = 'Total'       AND @SortDirection = 'DESC' THEN q.TotalAmount END DESC,
        CASE WHEN @SortBy = 'Validity'    AND @SortDirection = 'ASC'  THEN q.ValidityDateUtc END ASC,
        CASE WHEN @SortBy = 'Validity'    AND @SortDirection = 'DESC' THEN q.ValidityDateUtc END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN q.CreatedDateUtc END ASC,
        q.CreatedDateUtc DESC,
        q.QuotationId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- Draft -> Sent. Any other sent quotation for the booking is superseded; booking becomes Quoted.
CREATE OR ALTER PROCEDURE core.usp_Quotation_Send
    @QuotationId  BIGINT,
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @BookingStatusId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @BookingId = q.BookingId, @BookingStatusId = b.BookingStatusId
        FROM core.Quotation AS q WITH (UPDLOCK)
        INNER JOIN core.Booking AS b WITH (UPDLOCK) ON b.BookingId = q.BookingId
        WHERE q.QuotationId = @QuotationId AND q.QuotationStatusId = 1;

        IF @BookingId IS NULL
            THROW 50400, N'QUOTATION_NOT_DRAFT|Only draft quotations can be sent.', 1;

        IF @BookingStatusId NOT IN (3, 4)
            THROW 50400, N'BOOKING_NOT_QUOTABLE|The booking is no longer awaiting a quotation.', 1;

        IF EXISTS (SELECT 1 FROM core.Quotation WHERE QuotationId = @QuotationId AND ValidityDateUtc <= @Now)
            THROW 50400, N'QUOTATION_VALIDITY_PAST|The validity date is already in the past.', 1;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        SELECT q.QuotationId, 2, 6, N'Superseded by a newer quotation', @ModifiedBy, @Now
        FROM core.Quotation AS q WHERE q.BookingId = @BookingId AND q.QuotationStatusId = 2;

        UPDATE core.Quotation SET QuotationStatusId = 6, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE BookingId = @BookingId AND QuotationStatusId = 2;

        UPDATE core.Quotation
        SET QuotationStatusId = 2, SentDateUtc = @Now, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE QuotationId = @QuotationId;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@QuotationId, 1, 2, N'Sent to customer', @ModifiedBy, @Now);

        IF @BookingStatusId = 3
        BEGIN
            UPDATE core.Booking SET BookingStatusId = 4, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;
            INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@BookingId, 3, 4, N'Quotation sent', @ModifiedBy, @Now);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Customer accepts: Sent (and still valid) -> Accepted; booking Quoted -> Confirmed.
CREATE OR ALTER PROCEDURE core.usp_Quotation_Accept
    @QuotationId  BIGINT,
    @CustomerId   BIGINT,
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @BookingId = q.BookingId
        FROM core.Quotation AS q WITH (UPDLOCK)
        INNER JOIN core.Booking AS b WITH (UPDLOCK) ON b.BookingId = q.BookingId
        WHERE q.QuotationId = @QuotationId
          AND b.CustomerId = @CustomerId
          AND q.QuotationStatusId = 2
          AND q.ValidityDateUtc > @Now
          AND b.BookingStatusId = 4;

        IF @BookingId IS NULL
            THROW 50400, N'QUOTATION_NOT_ACCEPTABLE|This quotation can no longer be accepted (expired, superseded or already answered).', 1;

        UPDATE core.Quotation SET QuotationStatusId = 3, RespondedDateUtc = @Now, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE QuotationId = @QuotationId;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@QuotationId, 2, 3, N'Accepted by customer', @ModifiedBy, @Now);

        UPDATE core.Booking SET BookingStatusId = 5, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, 4, 5, N'Quotation accepted', @ModifiedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Customer rejects: Sent -> Rejected; booking goes back to UnderReview for a revised quotation.
CREATE OR ALTER PROCEDURE core.usp_Quotation_Reject
    @QuotationId  BIGINT,
    @CustomerId   BIGINT,
    @Reason       NVARCHAR(500),
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @BookingId = q.BookingId
        FROM core.Quotation AS q WITH (UPDLOCK)
        INNER JOIN core.Booking AS b WITH (UPDLOCK) ON b.BookingId = q.BookingId
        WHERE q.QuotationId = @QuotationId AND b.CustomerId = @CustomerId AND q.QuotationStatusId = 2 AND b.BookingStatusId = 4;

        IF @BookingId IS NULL
            THROW 50400, N'QUOTATION_NOT_REJECTABLE|This quotation is not awaiting your response.', 1;

        UPDATE core.Quotation
        SET QuotationStatusId = 4, RejectionReason = @Reason, RespondedDateUtc = @Now, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE QuotationId = @QuotationId;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@QuotationId, 2, 4, @Reason, @ModifiedBy, @Now);

        UPDATE core.Booking SET BookingStatusId = 3, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, 4, 3, N'Quotation rejected by customer', @ModifiedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Quotation_Withdraw
    @QuotationId  BIGINT,
    @Reason       NVARCHAR(500),
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @FromStatusId INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @BookingId = BookingId, @FromStatusId = QuotationStatusId
        FROM core.Quotation WITH (UPDLOCK)
        WHERE QuotationId = @QuotationId AND QuotationStatusId IN (1, 2);

        IF @BookingId IS NULL
            THROW 50400, N'QUOTATION_NOT_WITHDRAWABLE|Only draft or sent quotations can be withdrawn.', 1;

        UPDATE core.Quotation SET QuotationStatusId = 6, Notes = ISNULL(Notes + N' | ', N'') + @Reason, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE QuotationId = @QuotationId;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@QuotationId, @FromStatusId, 6, @Reason, @ModifiedBy, @Now);

        -- if the customer no longer has any open quotation, the booking returns to review
        IF @FromStatusId = 2 AND NOT EXISTS (SELECT 1 FROM core.Quotation WHERE BookingId = @BookingId AND QuotationStatusId = 2)
        BEGIN
            UPDATE core.Booking SET BookingStatusId = 3, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
            WHERE BookingId = @BookingId AND BookingStatusId = 4;

            IF @@ROWCOUNT > 0
                INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
                VALUES (@BookingId, 4, 3, N'Quotation withdrawn', @ModifiedBy, @Now);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Background job: expire sent quotations past their validity. Returns number expired.
CREATE OR ALTER PROCEDURE core.usp_Quotation_ExpireOverdue
    @AsOfUtc DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Expired TABLE (QuotationId BIGINT PRIMARY KEY, BookingId BIGINT NOT NULL);

    BEGIN TRANSACTION;
        UPDATE core.Quotation
        SET QuotationStatusId = 5, ModifiedDateUtc = @AsOfUtc
        OUTPUT inserted.QuotationId, inserted.BookingId INTO @Expired (QuotationId, BookingId)
        WHERE QuotationStatusId = 2 AND ValidityDateUtc <= @AsOfUtc;

        INSERT INTO core.QuotationStatusHistory (QuotationId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        SELECT e.QuotationId, 2, 5, N'Validity expired', NULL, @AsOfUtc FROM @Expired AS e;

        DECLARE @Reverted TABLE (BookingId BIGINT PRIMARY KEY);

        UPDATE b
        SET BookingStatusId = 3, ModifiedDateUtc = @AsOfUtc
        OUTPUT inserted.BookingId INTO @Reverted (BookingId)
        FROM core.Booking AS b
        WHERE b.BookingStatusId = 4
          AND b.BookingId IN (SELECT BookingId FROM @Expired)
          AND NOT EXISTS (SELECT 1 FROM core.Quotation AS q WHERE q.BookingId = b.BookingId AND q.QuotationStatusId = 2);

        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        SELECT r.BookingId, 4, 3, N'Quotation expired', NULL, @AsOfUtc FROM @Reverted AS r;
    COMMIT TRANSACTION;

    SELECT COUNT(*) AS Value FROM @Expired;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Quotation_GetStatusHistory
    @QuotationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.QuotationStatusHistoryId AS HistoryId, h.FromStatusId, h.ToStatusId, h.Remarks, h.ChangedBy, u.FullName AS ChangedByName, h.ChangedDateUtc
    FROM core.QuotationStatusHistory AS h
    LEFT JOIN sec.[User] AS u ON u.UserId = h.ChangedBy
    WHERE h.QuotationId = @QuotationId
    ORDER BY h.ChangedDateUtc, h.QuotationStatusHistoryId;
END
GO
