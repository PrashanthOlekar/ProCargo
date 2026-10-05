/*
    Module: Invoices, payments and refunds (schema fin)
    Invoice status: 1 Draft, 2 Issued, 3 PartiallyPaid, 4 Paid, 5 Cancelled
    Payment status: 1 Pending, 2 Initiated, 3 Authorized, 4 Paid, 5 Failed, 6 Refunded, 7 PartiallyRefunded, 8 PartiallyPaid, 9 Cancelled
    Refund status : 1 Pending, 2 Processed, 3 Failed

    Refunds are tracked against the payment (customer compensation / overcharge) and do NOT reopen the
    invoice; amount corrections to an invoice are made with invoice adjustments (credit lines).
*/

CREATE OR ALTER PROCEDURE fin.usp_Invoice_Create
    @BookingId          BIGINT,
    @PaymentTermsDays   INT,
    @CreatedBy          BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @TripId BIGINT, @TripStatusId INT, @CustomerId BIGINT, @QuotationId BIGINT;
    DECLARE @InvoiceId BIGINT, @InvoiceNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @CustomerId = b.CustomerId
        FROM core.Booking AS b WITH (UPDLOCK)
        WHERE b.BookingId = @BookingId AND b.BookingStatusId = 8;

        IF @CustomerId IS NULL
            THROW 50400, N'BOOKING_NOT_DELIVERED|Invoices can only be generated for delivered bookings.', 1;

        SELECT @TripId = t.TripId, @TripStatusId = t.TripStatusId, @QuotationId = t.QuotationId
        FROM core.Trip AS t WITH (UPDLOCK)
        WHERE t.BookingId = @BookingId AND t.TripStatusId <> 8;

        IF @TripStatusId IS NULL OR @TripStatusId <> 5
            THROW 50400, N'POD_REQUIRED|Proof of delivery must be uploaded before invoicing.', 1;

        IF EXISTS (SELECT 1 FROM fin.Invoice WHERE BookingId = @BookingId AND InvoiceStatusId <> 5)
            THROW 50409, N'INVOICE_EXISTS|An invoice already exists for this booking.', 1;

        SET @InvoiceNumber = core.fn_FormatBusinessNumber('INV', NEXT VALUE FOR fin.seq_InvoiceNumber, @Now);

        INSERT INTO fin.Invoice (InvoiceNumber, BookingId, TripId, CustomerId, QuotationId, InvoiceDateUtc, DueDateUtc, SubTotal, TaxPercent, TaxAmount, TotalAmount, InvoiceStatusId, CreatedBy, CreatedDateUtc)
        SELECT @InvoiceNumber, @BookingId, @TripId, @CustomerId, q.QuotationId, @Now, DATEADD(DAY, @PaymentTermsDays, @Now),
               q.SubTotal, q.TaxPercent, q.TaxAmount, q.TotalAmount, 2, @CreatedBy, @Now
        FROM core.Quotation AS q
        WHERE q.QuotationId = @QuotationId;
        SET @InvoiceId = SCOPE_IDENTITY();

        INSERT INTO fin.InvoiceItem (InvoiceId, ChargeCode, Description, Quantity, UnitRate, Amount, IsAdjustment, SortOrder, CreatedBy)
        SELECT @InvoiceId, qc.ChargeCode, qc.Description, qc.Quantity, qc.UnitRate, qc.Amount, 0, qc.SortOrder, @CreatedBy
        FROM core.QuotationCharge AS qc
        WHERE qc.QuotationId = @QuotationId;

        UPDATE core.Booking SET BookingStatusId = 9, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId;
        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, 8, 9, N'Invoice ' + @InvoiceNumber + N' issued', @CreatedBy, @Now);

        UPDATE core.Trip SET TripStatusId = 6, ModifiedBy = @CreatedBy, ModifiedDateUtc = @Now WHERE TripId = @TripId;
        INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@TripId, 5, 6, N'Invoiced', @CreatedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @InvoiceId AS Id, @InvoiceNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Invoice_GetById
    @InvoiceId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        i.InvoiceId,
        i.InvoiceNumber,
        i.BookingId,
        b.BookingNumber,
        i.TripId,
        t.TripNumber,
        i.CustomerId,
        c.FullName     AS CustomerName,
        c.CompanyName  AS CustomerCompanyName,
        c.GstNumber    AS CustomerGstNumber,
        c.Email        AS CustomerEmail,
        i.QuotationId,
        i.InvoiceDateUtc,
        i.DueDateUtc,
        i.SubTotal,
        i.TaxPercent,
        i.TaxAmount,
        i.TotalAmount,
        i.PaidAmount,
        CAST(i.TotalAmount - i.PaidAmount AS DECIMAL(14,2)) AS BalanceAmount,
        i.InvoiceStatusId,
        i.CancellationReason,
        i.CreatedDateUtc,
        i.RowVersion
    FROM fin.Invoice AS i
    INNER JOIN core.Booking  AS b ON b.BookingId = i.BookingId
    INNER JOIN core.Trip     AS t ON t.TripId = i.TripId
    INNER JOIN core.Customer AS c ON c.CustomerId = i.CustomerId
    WHERE i.InvoiceId = @InvoiceId;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_InvoiceItem_GetByInvoice
    @InvoiceId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ii.InvoiceItemId, ii.ChargeCode, ii.Description, ii.Quantity, ii.UnitRate, ii.Amount, ii.IsAdjustment, ii.SortOrder
    FROM fin.InvoiceItem AS ii
    WHERE ii.InvoiceId = @InvoiceId
    ORDER BY ii.SortOrder, ii.InvoiceItemId;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Invoice_GetPaged
    @PageNumber       INT,
    @PageSize         INT,
    @Search           NVARCHAR(100) = NULL,
    @CustomerId       BIGINT        = NULL,
    @InvoiceStatusId  INT           = NULL,
    @OverdueOnly      BIT           = 0,
    @FromDateUtc      DATETIME2(3)  = NULL,
    @ToDateUtc        DATETIME2(3)  = NULL,
    @SortBy           VARCHAR(50)   = 'InvoiceDate',
    @SortDirection    VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();

    SELECT
        i.InvoiceId,
        i.InvoiceNumber,
        i.BookingId,
        b.BookingNumber,
        i.CustomerId,
        c.FullName AS CustomerName,
        i.InvoiceDateUtc,
        i.DueDateUtc,
        i.TotalAmount,
        i.PaidAmount,
        i.InvoiceStatusId,
        CAST(CASE WHEN i.InvoiceStatusId IN (2, 3) AND i.DueDateUtc < @Now THEN 1 ELSE 0 END AS BIT) AS IsOverdue,
        COUNT(*) OVER () AS TotalRecords
    FROM fin.Invoice AS i
    INNER JOIN core.Booking  AS b ON b.BookingId = i.BookingId
    INNER JOIN core.Customer AS c ON c.CustomerId = i.CustomerId
    WHERE (@CustomerId IS NULL OR i.CustomerId = @CustomerId)
      AND (@InvoiceStatusId IS NULL OR i.InvoiceStatusId = @InvoiceStatusId)
      AND (@OverdueOnly = 0 OR (i.InvoiceStatusId IN (2, 3) AND i.DueDateUtc < @Now))
      AND (@FromDateUtc IS NULL OR i.InvoiceDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR i.InvoiceDateUtc < @ToDateUtc)
      AND (@Search IS NULL OR i.InvoiceNumber LIKE @Search + '%' OR b.BookingNumber LIKE @Search + '%' OR c.FullName LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'DueDate'     AND @SortDirection = 'ASC'  THEN i.DueDateUtc END ASC,
        CASE WHEN @SortBy = 'DueDate'     AND @SortDirection = 'DESC' THEN i.DueDateUtc END DESC,
        CASE WHEN @SortBy = 'Total'       AND @SortDirection = 'ASC'  THEN i.TotalAmount END ASC,
        CASE WHEN @SortBy = 'Total'       AND @SortDirection = 'DESC' THEN i.TotalAmount END DESC,
        CASE WHEN @SortBy = 'InvoiceDate' AND @SortDirection = 'ASC'  THEN i.InvoiceDateUtc END ASC,
        i.InvoiceDateUtc DESC,
        i.InvoiceId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

-- Adds a signed adjustment line (e.g. waiting charges incurred, goodwill credit) and recalculates totals.
CREATE OR ALTER PROCEDURE fin.usp_Invoice_AddAdjustment
    @InvoiceId    BIGINT,
    @Description  NVARCHAR(200),
    @Amount       DECIMAL(14,2),
    @CreatedBy    BIGINT,
    @RowVersion   BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM fin.Invoice WITH (UPDLOCK) WHERE InvoiceId = @InvoiceId AND RowVersion = @RowVersion)
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM fin.Invoice WHERE InvoiceId = @InvoiceId)
                THROW 50404, N'INVOICE_NOT_FOUND|Invoice was not found.', 1;
            THROW 50412, N'CONCURRENCY_CONFLICT|The invoice was modified by someone else. Reload and try again.', 1;
        END

        IF NOT EXISTS (SELECT 1 FROM fin.Invoice WHERE InvoiceId = @InvoiceId AND InvoiceStatusId IN (2, 3))
            THROW 50400, N'INVOICE_NOT_ADJUSTABLE|Only issued or partially paid invoices can be adjusted.', 1;

        INSERT INTO fin.InvoiceItem (InvoiceId, ChargeCode, Description, Quantity, UnitRate, Amount, IsAdjustment, SortOrder, CreatedBy)
        VALUES (@InvoiceId, 'ADJUSTMENT', @Description, 1, @Amount, @Amount, 1, 1000, @CreatedBy);

        DECLARE @SubTotal DECIMAL(14,2) = (SELECT SUM(Amount) FROM fin.InvoiceItem WHERE InvoiceId = @InvoiceId AND ChargeCode <> 'TAX');

        UPDATE fin.Invoice
        SET SubTotal = @SubTotal,
            TaxAmount = ROUND(@SubTotal * TaxPercent / 100, 2),
            TotalAmount = @SubTotal + ROUND(@SubTotal * TaxPercent / 100, 2),
            ModifiedBy = @CreatedBy,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE InvoiceId = @InvoiceId;

        IF EXISTS (SELECT 1 FROM fin.Invoice WHERE InvoiceId = @InvoiceId AND (TotalAmount < PaidAmount OR SubTotal < 0))
            THROW 50400, N'ADJUSTMENT_INVALID|The adjustment would make the invoice total lower than the amount already paid.', 1;

        UPDATE fin.Invoice
        SET InvoiceStatusId = CASE WHEN PaidAmount = 0 THEN 2 WHEN PaidAmount < TotalAmount THEN 3 ELSE 4 END
        WHERE InvoiceId = @InvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Invoice_Cancel
    @InvoiceId   BIGINT,
    @Reason      NVARCHAR(500),
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @BookingId BIGINT, @TripId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE fin.Invoice
        SET @BookingId = BookingId, @TripId = TripId, InvoiceStatusId = 5, CancellationReason = @Reason,
            ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
        WHERE InvoiceId = @InvoiceId AND InvoiceStatusId = 2 AND PaidAmount = 0;

        IF @@ROWCOUNT = 0
            THROW 50400, N'INVOICE_NOT_CANCELLABLE|Only unpaid issued invoices can be cancelled.', 1;

        -- re-open billing: booking back to Delivered, trip back to PodUploaded
        UPDATE core.Booking SET BookingStatusId = 8, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now WHERE BookingId = @BookingId AND BookingStatusId = 9;
        INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@BookingId, 9, 8, N'Invoice cancelled: ' + @Reason, @ModifiedBy, @Now);

        UPDATE core.Trip SET TripStatusId = 5, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now WHERE TripId = @TripId AND TripStatusId = 6;
        INSERT INTO core.TripStatusHistory (TripId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
        VALUES (@TripId, 6, 5, N'Invoice cancelled', @ModifiedBy, @Now);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- Internal helper: applies a PAID payment to its invoice and moves the booking to Paid when settled in full.
-- Must be called inside the caller's transaction.
CREATE OR ALTER PROCEDURE fin.usp_Payment_ApplyToInvoice
    @PaymentId  BIGINT,
    @ChangedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @InvoiceId BIGINT, @Amount DECIMAL(14,2), @BookingId BIGINT, @NewStatus INT, @Now DATETIME2(3) = SYSUTCDATETIME();
    SELECT @InvoiceId = InvoiceId, @Amount = Amount, @BookingId = BookingId FROM fin.Payment WHERE PaymentId = @PaymentId;

    UPDATE fin.Invoice
    SET PaidAmount = PaidAmount + @Amount,
        @NewStatus = InvoiceStatusId = CASE WHEN PaidAmount + @Amount >= TotalAmount THEN 4 ELSE 3 END,
        ModifiedBy = @ChangedBy,
        ModifiedDateUtc = @Now
    WHERE InvoiceId = @InvoiceId AND InvoiceStatusId IN (2, 3) AND PaidAmount + @Amount <= TotalAmount;

    IF @@ROWCOUNT = 0
        THROW 50400, N'PAYMENT_EXCEEDS_BALANCE|The payment exceeds the outstanding invoice balance.', 1;

    IF @NewStatus = 4
    BEGIN
        UPDATE core.Booking SET BookingStatusId = 10, ModifiedBy = @ChangedBy, ModifiedDateUtc = @Now
        WHERE BookingId = @BookingId AND BookingStatusId = 9;

        IF @@ROWCOUNT > 0
            INSERT INTO core.BookingStatusHistory (BookingId, FromStatusId, ToStatusId, Remarks, ChangedBy, ChangedDateUtc)
            VALUES (@BookingId, 9, 10, N'Invoice paid in full', @ChangedBy, @Now);
    END
END
GO

/*
    Creates a payment. Idempotent per (CustomerId, IdempotencyKey): repeating the same request returns the
    original payment with IsExisting = 1 instead of creating a duplicate.
    @PaymentStatusId = 2 (Initiated) for online payments, 4 (Paid) for payments recorded by finance.
*/
CREATE OR ALTER PROCEDURE fin.usp_Payment_Create
    @InvoiceId        BIGINT,
    @Amount           DECIMAL(14,2),
    @PaymentMethodId  INT,
    @PaymentStatusId  INT,
    @GatewayName      VARCHAR(50),
    @IdempotencyKey   VARCHAR(100),
    @ReferenceNumber  VARCHAR(100),
    @Remarks          NVARCHAR(500),
    @CreatedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @CustomerId BIGINT, @BookingId BIGINT, @Balance DECIMAL(14,2);
    DECLARE @PaymentId BIGINT, @PaymentNumber VARCHAR(30), @IsExisting BIT = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @CustomerId = CustomerId, @BookingId = BookingId, @Balance = TotalAmount - PaidAmount
        FROM fin.Invoice WITH (UPDLOCK)
        WHERE InvoiceId = @InvoiceId AND InvoiceStatusId IN (2, 3);

        IF @CustomerId IS NULL
            THROW 50400, N'INVOICE_NOT_PAYABLE|The invoice is not open for payment.', 1;

        IF @IdempotencyKey IS NOT NULL
            SELECT @PaymentId = PaymentId, @PaymentNumber = PaymentNumber
            FROM fin.Payment WHERE CustomerId = @CustomerId AND IdempotencyKey = @IdempotencyKey;

        IF @PaymentId IS NOT NULL
            SET @IsExisting = 1;
        ELSE
        BEGIN
            IF @Amount > @Balance
                THROW 50400, N'PAYMENT_EXCEEDS_BALANCE|The payment exceeds the outstanding invoice balance.', 1;

            SET @PaymentNumber = core.fn_FormatBusinessNumber('PAY', NEXT VALUE FOR fin.seq_PaymentNumber, @Now);

            INSERT INTO fin.Payment (PaymentNumber, InvoiceId, BookingId, CustomerId, Amount, PaymentMethodId, PaymentStatusId, GatewayName,
                                     ReferenceNumber, IdempotencyKey, PaymentDateUtc, Remarks, CreatedBy, CreatedDateUtc)
            VALUES (@PaymentNumber, @InvoiceId, @BookingId, @CustomerId, @Amount, @PaymentMethodId, @PaymentStatusId, @GatewayName,
                    @ReferenceNumber, @IdempotencyKey, CASE WHEN @PaymentStatusId = 4 THEN @Now ELSE NULL END, @Remarks, @CreatedBy, @Now);
            SET @PaymentId = SCOPE_IDENTITY();

            INSERT INTO fin.PaymentAttempt (PaymentId, AttemptNo, Status, ResponseMessage)
            VALUES (@PaymentId, 1, CASE WHEN @PaymentStatusId = 4 THEN 'RecordedOffline' ELSE 'Initiated' END, NULL);

            IF @PaymentStatusId = 4
                EXEC fin.usp_Payment_ApplyToInvoice @PaymentId = @PaymentId, @ChangedBy = @CreatedBy;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @PaymentId AS Id, @PaymentNumber AS Number, @IsExisting AS IsExisting;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Payment_SetGatewayOrder
    @PaymentId       BIGINT,
    @GatewayOrderId  VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE fin.Payment SET GatewayOrderId = @GatewayOrderId, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE PaymentId = @PaymentId AND GatewayOrderId IS NULL;
END
GO

/*
    Gateway callback / webhook / manual status update. Idempotent per @GatewayEventId.
    Returns WasDuplicate = 1 (and changes nothing) when the event was already processed.
*/
CREATE OR ALTER PROCEDURE fin.usp_Payment_UpdateStatus
    @PaymentId             BIGINT,
    @NewStatusId           INT,
    @GatewayTransactionId  VARCHAR(100),
    @GatewayEventId        VARCHAR(100),
    @ResponseCode          VARCHAR(50),
    @ResponseMessage       NVARCHAR(500),
    @ModifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @CurrentStatusId INT, @WasDuplicate BIT = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @CurrentStatusId = PaymentStatusId FROM fin.Payment WITH (UPDLOCK) WHERE PaymentId = @PaymentId;
        IF @CurrentStatusId IS NULL
            THROW 50404, N'PAYMENT_NOT_FOUND|Payment was not found.', 1;

        IF @GatewayEventId IS NOT NULL AND EXISTS (SELECT 1 FROM fin.PaymentAttempt WHERE GatewayEventId = @GatewayEventId)
            SET @WasDuplicate = 1;
        ELSE IF @CurrentStatusId IN (4, 6, 7, 9) AND @NewStatusId <> @CurrentStatusId
            THROW 50409, N'PAYMENT_ALREADY_FINAL|The payment is already in a final state.', 1;
        ELSE IF @CurrentStatusId = @NewStatusId
            SET @WasDuplicate = 1;
        ELSE
        BEGIN
            INSERT INTO fin.PaymentAttempt (PaymentId, AttemptNo, GatewayEventId, Status, ResponseCode, ResponseMessage, CreatedDateUtc)
            SELECT @PaymentId, ISNULL(MAX(AttemptNo), 0) + 1, @GatewayEventId,
                   (SELECT Code FROM mst.PaymentStatus WHERE PaymentStatusId = @NewStatusId), @ResponseCode, @ResponseMessage, @Now
            FROM fin.PaymentAttempt WHERE PaymentId = @PaymentId;

            UPDATE fin.Payment
            SET PaymentStatusId = @NewStatusId,
                GatewayTransactionId = ISNULL(@GatewayTransactionId, GatewayTransactionId),
                PaymentDateUtc = CASE WHEN @NewStatusId = 4 THEN @Now ELSE PaymentDateUtc END,
                FailureReason = CASE WHEN @NewStatusId = 5 THEN @ResponseMessage ELSE FailureReason END,
                ModifiedBy = @ModifiedBy,
                ModifiedDateUtc = @Now
            WHERE PaymentId = @PaymentId;

            IF @NewStatusId = 4
                EXEC fin.usp_Payment_ApplyToInvoice @PaymentId = @PaymentId, @ChangedBy = @ModifiedBy;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @PaymentId AS Id, @WasDuplicate AS IsExisting;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Payment_GetById
    @PaymentId       BIGINT       = NULL,
    @GatewayName     VARCHAR(50)  = NULL,
    @GatewayOrderId  VARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.PaymentId,
        p.PaymentNumber,
        p.InvoiceId,
        i.InvoiceNumber,
        p.BookingId,
        b.BookingNumber,
        p.CustomerId,
        c.FullName AS CustomerName,
        p.Amount,
        p.RefundedAmount,
        p.PaymentMethodId,
        p.PaymentStatusId,
        p.GatewayName,
        p.GatewayOrderId,
        p.GatewayTransactionId,
        p.ReferenceNumber,
        p.PaymentDateUtc,
        p.FailureReason,
        p.Remarks,
        p.CreatedDateUtc
    FROM fin.Payment AS p
    INNER JOIN fin.Invoice   AS i ON i.InvoiceId = p.InvoiceId
    INNER JOIN core.Booking  AS b ON b.BookingId = p.BookingId
    INNER JOIN core.Customer AS c ON c.CustomerId = p.CustomerId
    WHERE (@PaymentId IS NOT NULL AND p.PaymentId = @PaymentId)
       OR (@PaymentId IS NULL AND p.GatewayName = @GatewayName AND p.GatewayOrderId = @GatewayOrderId);
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Payment_GetPaged
    @PageNumber       INT,
    @PageSize         INT,
    @Search           NVARCHAR(100) = NULL,
    @CustomerId       BIGINT        = NULL,
    @InvoiceId        BIGINT        = NULL,
    @PaymentStatusId  INT           = NULL,
    @PaymentMethodId  INT           = NULL,
    @FromDateUtc      DATETIME2(3)  = NULL,
    @ToDateUtc        DATETIME2(3)  = NULL,
    @SortBy           VARCHAR(50)   = 'CreatedDate',
    @SortDirection    VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.PaymentId,
        p.PaymentNumber,
        p.InvoiceId,
        i.InvoiceNumber,
        p.CustomerId,
        c.FullName AS CustomerName,
        p.Amount,
        p.RefundedAmount,
        p.PaymentMethodId,
        p.PaymentStatusId,
        p.GatewayName,
        p.PaymentDateUtc,
        p.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM fin.Payment AS p
    INNER JOIN fin.Invoice   AS i ON i.InvoiceId = p.InvoiceId
    INNER JOIN core.Customer AS c ON c.CustomerId = p.CustomerId
    WHERE (@CustomerId IS NULL OR p.CustomerId = @CustomerId)
      AND (@InvoiceId IS NULL OR p.InvoiceId = @InvoiceId)
      AND (@PaymentStatusId IS NULL OR p.PaymentStatusId = @PaymentStatusId)
      AND (@PaymentMethodId IS NULL OR p.PaymentMethodId = @PaymentMethodId)
      AND (@FromDateUtc IS NULL OR p.CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR p.CreatedDateUtc < @ToDateUtc)
      AND (@Search IS NULL OR p.PaymentNumber LIKE @Search + '%' OR i.InvoiceNumber LIKE @Search + '%'
           OR p.GatewayTransactionId LIKE @Search + '%' OR p.ReferenceNumber LIKE @Search + '%' OR c.FullName LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'Amount'      AND @SortDirection = 'ASC'  THEN p.Amount END ASC,
        CASE WHEN @SortBy = 'Amount'      AND @SortDirection = 'DESC' THEN p.Amount END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN p.CreatedDateUtc END ASC,
        p.CreatedDateUtc DESC,
        p.PaymentId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE fin.usp_PaymentRefund_Create
    @PaymentId  BIGINT,
    @Amount     DECIMAL(14,2),
    @Reason     NVARCHAR(500),
    @CreatedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Refundable DECIMAL(14,2), @RefundId BIGINT, @RefundNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @Refundable = p.Amount - p.RefundedAmount
               - ISNULL((SELECT SUM(r.Amount) FROM fin.PaymentRefund AS r WHERE r.PaymentId = p.PaymentId AND r.RefundStatusId = 1), 0)
        FROM fin.Payment AS p WITH (UPDLOCK)
        WHERE p.PaymentId = @PaymentId AND p.PaymentStatusId IN (4, 7);

        IF @Refundable IS NULL
            THROW 50400, N'PAYMENT_NOT_REFUNDABLE|Only paid payments can be refunded.', 1;

        IF @Amount > @Refundable
            THROW 50400, N'REFUND_EXCEEDS_PAYMENT|The refund exceeds the refundable amount.', 1;

        SET @RefundNumber = core.fn_FormatBusinessNumber('RFD', NEXT VALUE FOR fin.seq_RefundNumber, @Now);

        INSERT INTO fin.PaymentRefund (RefundNumber, PaymentId, Amount, Reason, RefundStatusId, CreatedBy, CreatedDateUtc)
        VALUES (@RefundNumber, @PaymentId, @Amount, @Reason, 1, @CreatedBy, @Now);
        SET @RefundId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @RefundId AS Id, @RefundNumber AS Number;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_PaymentRefund_Complete
    @PaymentRefundId  BIGINT,
    @IsSuccess        BIT,
    @GatewayRefundId  VARCHAR(100),
    @FailureReason    NVARCHAR(500),
    @ModifiedBy       BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @PaymentId BIGINT, @Amount DECIMAL(14,2);

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE fin.PaymentRefund
        SET @PaymentId = PaymentId, @Amount = Amount,
            RefundStatusId = CASE WHEN @IsSuccess = 1 THEN 2 ELSE 3 END,
            GatewayRefundId = @GatewayRefundId,
            FailureReason = CASE WHEN @IsSuccess = 1 THEN NULL ELSE @FailureReason END,
            ProcessedDateUtc = @Now,
            ModifiedBy = @ModifiedBy,
            ModifiedDateUtc = @Now
        WHERE PaymentRefundId = @PaymentRefundId AND RefundStatusId = 1;

        IF @@ROWCOUNT = 0
            THROW 50400, N'REFUND_NOT_PENDING|The refund is not pending.', 1;

        IF @IsSuccess = 1
            UPDATE fin.Payment
            SET RefundedAmount = RefundedAmount + @Amount,
                PaymentStatusId = CASE WHEN RefundedAmount + @Amount >= Amount THEN 6 ELSE 7 END,
                ModifiedBy = @ModifiedBy,
                ModifiedDateUtc = @Now
            WHERE PaymentId = @PaymentId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE fin.usp_PaymentRefund_GetByPayment
    @PaymentId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.PaymentRefundId, r.RefundNumber, r.PaymentId, r.Amount, r.Reason, r.RefundStatusId, r.GatewayRefundId,
           r.ProcessedDateUtc, r.FailureReason, r.CreatedDateUtc
    FROM fin.PaymentRefund AS r
    WHERE r.PaymentId = @PaymentId
    ORDER BY r.CreatedDateUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Payment_GetReconciliation
    @FromUtc DATETIME2(3),
    @ToUtc   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.PaymentMethodId,
        pm.Name AS PaymentMethodName,
        p.GatewayName,
        COUNT(*) AS PaymentCount,
        CAST(SUM(CASE WHEN p.PaymentStatusId IN (4, 6, 7) THEN p.Amount ELSE 0 END) AS DECIMAL(14,2)) AS CollectedAmount,
        CAST(SUM(p.RefundedAmount) AS DECIMAL(14,2)) AS RefundedAmount,
        CAST(SUM(CASE WHEN p.PaymentStatusId IN (4, 6, 7) THEN p.Amount ELSE 0 END) - SUM(p.RefundedAmount) AS DECIMAL(14,2)) AS NetAmount,
        SUM(CASE WHEN p.PaymentStatusId = 5 THEN 1 ELSE 0 END) AS FailedCount,
        SUM(CASE WHEN p.PaymentStatusId IN (1, 2, 3) THEN 1 ELSE 0 END) AS PendingCount
    FROM fin.Payment AS p
    INNER JOIN mst.PaymentMethod AS pm ON pm.PaymentMethodId = p.PaymentMethodId
    WHERE p.CreatedDateUtc >= @FromUtc AND p.CreatedDateUtc < @ToUtc
    GROUP BY p.PaymentMethodId, pm.Name, p.GatewayName
    ORDER BY CollectedAmount DESC;
END
GO
