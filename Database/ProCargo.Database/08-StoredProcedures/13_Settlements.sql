/*
    Module: Owner settlements (schema fin)
    Settlement status: 1 Pending, 2 Approved, 3 Processing, 4 Completed, 5 Failed, 6 Cancelled

    Gross       = invoice sub-total (freight before tax) of the trip's booking
    Commission  = max(Gross * CommissionPercent, MinimumCommission), never more than Gross
    Tax (TDS)   = (Gross - Commission) * TdsPercent
    Net         = Gross - Commission - Tax + Adjustments
    Commission and TDS percentages are resolved by the API from fin.CommissionRule / mst.SystemSetting.
*/

CREATE OR ALTER PROCEDURE fin.usp_Settlement_Create
    @TripId             BIGINT,
    @CommissionPercent  DECIMAL(5,2),
    @MinimumCommission  DECIMAL(12,2),
    @TdsPercent         DECIMAL(5,2),
    @CreatedBy          BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @OwnerId BIGINT, @Gross DECIMAL(14,2), @TripNumber VARCHAR(30);
    DECLARE @Commission DECIMAL(14,2), @Tax DECIMAL(14,2), @Net DECIMAL(14,2), @BankAccountId BIGINT;
    DECLARE @SettlementId BIGINT, @SettlementNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @OwnerId = t.OwnerId, @Gross = i.SubTotal, @TripNumber = t.TripNumber
        FROM core.Trip AS t WITH (UPDLOCK)
        INNER JOIN fin.Invoice AS i ON i.TripId = t.TripId AND i.InvoiceStatusId = 4
        WHERE t.TripId = @TripId AND t.TripStatusId = 6;

        IF @OwnerId IS NULL
            THROW 50400, N'TRIP_NOT_SETTLEABLE|A settlement needs a completed trip whose invoice is fully paid.', 1;

        IF EXISTS (SELECT 1 FROM fin.Settlement WHERE TripId = @TripId AND SettlementStatusId NOT IN (5, 6))
            THROW 50409, N'SETTLEMENT_EXISTS|A settlement already exists for this trip.', 1;

        SET @Commission = ROUND(@Gross * @CommissionPercent / 100, 2);
        IF @Commission < @MinimumCommission SET @Commission = @MinimumCommission;
        IF @Commission > @Gross SET @Commission = @Gross;
        SET @Tax = ROUND((@Gross - @Commission) * @TdsPercent / 100, 2);
        SET @Net = @Gross - @Commission - @Tax;

        SELECT TOP (1) @BankAccountId = OwnerBankAccountId
        FROM core.OwnerBankAccount
        WHERE OwnerId = @OwnerId AND IsActive = 1
        ORDER BY IsPrimary DESC, CASE WHEN VerificationStatusId = 3 THEN 0 ELSE 1 END, CreatedDateUtc DESC;

        SET @SettlementNumber = core.fn_FormatBusinessNumber('STL', NEXT VALUE FOR fin.seq_SettlementNumber, @Now);

        INSERT INTO fin.Settlement (SettlementNumber, TripId, OwnerId, OwnerBankAccountId, GrossAmount, CommissionPercent, CommissionAmount,
                                    TaxAmount, AdjustmentAmount, NetAmount, SettlementStatusId, CreatedBy, CreatedDateUtc)
        VALUES (@SettlementNumber, @TripId, @OwnerId, @BankAccountId, @Gross, @CommissionPercent, @Commission,
                @Tax, 0, @Net, 1, @CreatedBy, @Now);
        SET @SettlementId = SCOPE_IDENTITY();

        INSERT INTO fin.SettlementItem (SettlementId, ItemType, Description, Amount, SortOrder, CreatedBy)
        VALUES (@SettlementId, 'Freight',    N'Freight for trip ' + @TripNumber, @Gross, 1, @CreatedBy),
               (@SettlementId, 'Commission', N'Platform commission (' + CAST(@CommissionPercent AS NVARCHAR(10)) + N'%)', -@Commission, 2, @CreatedBy),
               (@SettlementId, 'Tax',        N'TDS (' + CAST(@TdsPercent AS NVARCHAR(10)) + N'%)', -@Tax, 3, @CreatedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @SettlementId AS Id, @SettlementNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Settlement_AddAdjustment
    @SettlementId  BIGINT,
    @Description   NVARCHAR(200),
    @Amount        DECIMAL(14,2),
    @CreatedBy     BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE fin.Settlement
        SET AdjustmentAmount = AdjustmentAmount + @Amount,
            NetAmount = NetAmount + @Amount,
            ModifiedBy = @CreatedBy,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE SettlementId = @SettlementId AND SettlementStatusId = 1 AND NetAmount + @Amount >= 0;

        IF @@ROWCOUNT = 0
            THROW 50400, N'SETTLEMENT_NOT_ADJUSTABLE|Only pending settlements can be adjusted and the net amount cannot become negative.', 1;

        INSERT INTO fin.SettlementItem (SettlementId, ItemType, Description, Amount, SortOrder, CreatedBy)
        VALUES (@SettlementId, 'Adjustment', @Description, @Amount, 100, @CreatedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Approve (1->2), process (2->3), fail (3->5), cancel (1/2->6), retry processing (5->3 is not allowed: create a new settlement).
CREATE OR ALTER PROCEDURE fin.usp_Settlement_ChangeStatus
    @SettlementId      BIGINT,
    @ExpectedStatusId  INT,
    @NewStatusId       INT,
    @Remarks           NVARCHAR(500),
    @ChangedBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @NewStatusId = 3 AND EXISTS (SELECT 1 FROM fin.Settlement WHERE SettlementId = @SettlementId AND OwnerBankAccountId IS NULL)
    BEGIN
        -- pick up a bank account the owner may have added after the settlement was created
        UPDATE s SET OwnerBankAccountId =
            (SELECT TOP (1) ba.OwnerBankAccountId FROM core.OwnerBankAccount AS ba
             WHERE ba.OwnerId = s.OwnerId AND ba.IsActive = 1 ORDER BY ba.IsPrimary DESC, ba.CreatedDateUtc DESC)
        FROM fin.Settlement AS s WHERE s.SettlementId = @SettlementId;

        IF EXISTS (SELECT 1 FROM fin.Settlement WHERE SettlementId = @SettlementId AND OwnerBankAccountId IS NULL)
            THROW 50400, N'OWNER_BANK_ACCOUNT_REQUIRED|The owner has no active bank account to pay into.', 1;
    END

    UPDATE fin.Settlement
    SET SettlementStatusId = @NewStatusId,
        ApprovedBy = CASE WHEN @NewStatusId = 2 THEN @ChangedBy ELSE ApprovedBy END,
        ApprovedDateUtc = CASE WHEN @NewStatusId = 2 THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,
        FailureReason = CASE WHEN @NewStatusId IN (5, 6) THEN @Remarks ELSE FailureReason END,
        ModifiedBy = @ChangedBy,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SettlementId = @SettlementId AND SettlementStatusId = @ExpectedStatusId;

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM fin.Settlement WHERE SettlementId = @SettlementId)
            THROW 50404, N'SETTLEMENT_NOT_FOUND|Settlement was not found.', 1;
        THROW 50409, N'SETTLEMENT_STATUS_CHANGED|The settlement status was changed by another request. Reload and try again.', 1;
    END
END
GO

-- Processing -> Completed; closes the trip and, if the booking is paid, the booking too.
CREATE OR ALTER PROCEDURE fin.usp_Settlement_Complete
    @SettlementId          BIGINT,
    @TransactionReference  VARCHAR(100),
    @ChangedBy             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @TripId BIGINT, @BookingId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE fin.Settlement
        SET @TripId = TripId, SettlementStatusId = 4, SettlementDateUtc = @Now, TransactionReference = @TransactionReference,
            ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
        WHERE SettlementId = @SettlementId AND SettlementStatusId = 3;

        IF @@ROWCOUNT = 0
            THROW 50400, N'SETTLEMENT_NOT_PROCESSING|Only settlements in processing can be completed.', 1;

        UPDATE core.Trip SET @BookingId = BookingId, TripStatusId = 7, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
        WHERE TripId = @TripId AND TripStatusId = 6;

        IF @@ROWCOUNT > 0
            INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@TripId, 6, 7, N'Owner settled', @ChangedBy, @Now);

        IF @BookingId IS NOT NULL
        BEGIN
            UPDATE core.Booking SET BookingStatusId = 11, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
            WHERE BookingId = @BookingId AND BookingStatusId = 10;

            IF @@ROWCOUNT > 0
                INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
                VALUES (@BookingId, 10, 11, N'Trip settled and closed', @ChangedBy, @Now);
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Settlement_GetById
    @SettlementId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.SettlementId,
        s.SettlementNumber,
        s.TripId,
        t.TripNumber,
        b.BookingNumber,
        s.OwnerId,
        o.FullName AS OwnerName,
        s.OwnerBankAccountId,
        ba.BankName,
        ba.AccountNumberLast4,
        s.GrossAmount,
        s.CommissionPercent,
        s.CommissionAmount,
        s.TaxAmount,
        s.AdjustmentAmount,
        s.NetAmount,
        s.SettlementStatusId,
        s.CreatedBy,
        o.UserId AS OwnerUserId,
        s.ApprovedDateUtc,
        s.SettlementDateUtc,
        s.TransactionReference,
        s.FailureReason,
        s.CreatedDateUtc,
        s.RowVersion
    FROM fin.Settlement AS s
    INNER JOIN core.Trip         AS t  ON t.TripId = s.TripId
    INNER JOIN core.Booking      AS b  ON b.BookingId = t.BookingId
    INNER JOIN core.VehicleOwner AS o  ON o.OwnerId = s.OwnerId
    LEFT  JOIN core.OwnerBankAccount AS ba ON ba.OwnerBankAccountId = s.OwnerBankAccountId
    WHERE s.SettlementId = @SettlementId;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_SettlementItem_GetBySettlement
    @SettlementId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.SettlementItemId, si.ItemType, si.Description, si.Amount, si.SortOrder
    FROM fin.SettlementItem AS si
    WHERE si.SettlementId = @SettlementId
    ORDER BY si.SortOrder, si.SettlementItemId;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Settlement_GetPaged
    @PageNumber          INT,
    @PageSize            INT,
    @Search              NVARCHAR(100) = NULL,
    @OwnerId             BIGINT        = NULL,
    @SettlementStatusId  INT           = NULL,
    @FromDateUtc         DATETIME2(3)  = NULL,
    @ToDateUtc           DATETIME2(3)  = NULL,
    @SortBy              VARCHAR(50)   = 'CreatedDate',
    @SortDirection       VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        s.SettlementId,
        s.SettlementNumber,
        s.TripId,
        t.TripNumber,
        s.OwnerId,
        o.FullName AS OwnerName,
        s.GrossAmount,
        s.CommissionAmount,
        s.NetAmount,
        s.SettlementStatusId,
        s.SettlementDateUtc,
        s.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM fin.Settlement AS s
    INNER JOIN core.Trip         AS t ON t.TripId = s.TripId
    INNER JOIN core.VehicleOwner AS o ON o.OwnerId = s.OwnerId
    WHERE (@OwnerId IS NULL OR s.OwnerId = @OwnerId)
      AND (@SettlementStatusId IS NULL OR s.SettlementStatusId = @SettlementStatusId)
      AND (@FromDateUtc IS NULL OR s.CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR s.CreatedDateUtc < @ToDateUtc)
      AND (@Search IS NULL OR s.SettlementNumber LIKE @Search + '%' OR t.TripNumber LIKE @Search + '%' OR o.FullName LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'Net'         AND @SortDirection = 'ASC'  THEN s.NetAmount END ASC,
        CASE WHEN @SortBy = 'Net'         AND @SortDirection = 'DESC' THEN s.NetAmount END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN s.CreatedDateUtc END ASC,
        s.CreatedDateUtc DESC,
        s.SettlementId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Settlement_GetOwnerSummary
    @OwnerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(ISNULL(SUM(CASE WHEN s.SettlementStatusId = 4 THEN s.NetAmount END), 0) AS DECIMAL(14,2)) AS TotalEarned,
        CAST(ISNULL(SUM(CASE WHEN s.SettlementStatusId IN (1, 2, 3) THEN s.NetAmount END), 0) AS DECIMAL(14,2)) AS PendingAmount,
        ISNULL(SUM(CASE WHEN s.SettlementStatusId = 4 THEN 1 ELSE 0 END), 0) AS CompletedCount,
        ISNULL(SUM(CASE WHEN s.SettlementStatusId IN (1, 2, 3) THEN 1 ELSE 0 END), 0) AS PendingCount,
        MAX(s.SettlementDateUtc) AS LastSettlementDateUtc
    FROM fin.Settlement AS s
    WHERE s.OwnerId = @OwnerId;
END
GO

-- Completed trips with a fully paid invoice and no live settlement: the Finance "ready to settle" queue.
CREATE OR ALTER PROCEDURE fin.usp_Settlement_GetEligibleTrips
    @PageNumber INT,
    @PageSize   INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.TripId,
        t.TripNumber,
        b.BookingNumber,
        t.OwnerId,
        o.FullName AS OwnerName,
        v.VehicleTypeId,
        i.SubTotal AS InvoiceSubTotal,
        t.ActualDeliveryDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Trip AS t
    INNER JOIN core.Booking      AS b ON b.BookingId = t.BookingId
    INNER JOIN core.VehicleOwner AS o ON o.OwnerId = t.OwnerId
    INNER JOIN core.Vehicle      AS v ON v.VehicleId = t.VehicleId
    INNER JOIN fin.Invoice       AS i ON i.TripId = t.TripId AND i.InvoiceStatusId = 4
    WHERE t.TripStatusId = 6
      AND NOT EXISTS (SELECT 1 FROM fin.Settlement AS s WHERE s.TripId = t.TripId AND s.SettlementStatusId NOT IN (5, 6))
    ORDER BY t.ActualDeliveryDateUtc, t.TripId
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO
