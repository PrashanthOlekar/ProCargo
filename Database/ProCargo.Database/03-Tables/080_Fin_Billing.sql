/*
    Invoices, payments, refunds and owner settlements.
    Financial rows are never physically deleted; they move to Cancelled/Failed states.
    Idempotency:
      * Payment.IdempotencyKey (unique)     - duplicate "create payment" requests return the original payment
      * PaymentAttempt.GatewayEventId       - duplicate gateway webhooks are ignored
      * one active Settlement per Trip       - enforced by a filtered unique index (05-Indexes)
*/

IF OBJECT_ID(N'fin.Invoice', N'U') IS NULL
BEGIN
    CREATE TABLE fin.Invoice
    (
        InvoiceId           BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Invoice PRIMARY KEY,
        InvoiceNumber       VARCHAR(30)    NOT NULL CONSTRAINT UQ_Invoice_InvoiceNumber UNIQUE,
        BookingId           BIGINT         NOT NULL,
        TripId              BIGINT         NOT NULL,
        CustomerId          BIGINT         NOT NULL,
        QuotationId         BIGINT         NOT NULL,
        InvoiceDateUtc      DATETIME2(3)   NOT NULL,
        DueDateUtc          DATETIME2(3)   NOT NULL,
        SubTotal            DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Invoice_SubTotal CHECK (SubTotal >= 0),
        TaxPercent          DECIMAL(5,2)   NOT NULL,
        TaxAmount           DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Invoice_TaxAmount CHECK (TaxAmount >= 0),
        TotalAmount         DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Invoice_TotalAmount CHECK (TotalAmount >= 0),
        PaidAmount          DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Invoice_PaidAmount DEFAULT (0),
        InvoiceStatusId     INT            NOT NULL,
        CancellationReason  NVARCHAR(500)  NULL,
        CreatedBy           BIGINT         NULL,
        CreatedDateUtc      DATETIME2(3)   NOT NULL CONSTRAINT DF_Invoice_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy          BIGINT         NULL,
        ModifiedDateUtc     DATETIME2(3)   NULL,
        RowVersion          ROWVERSION     NOT NULL,
        CONSTRAINT CK_Invoice_PaidAmount CHECK (PaidAmount >= 0 AND PaidAmount <= TotalAmount),
        CONSTRAINT CK_Invoice_DueDate CHECK (DueDateUtc >= InvoiceDateUtc)
    );
END
GO

IF OBJECT_ID(N'fin.InvoiceItem', N'U') IS NULL
BEGIN
    CREATE TABLE fin.InvoiceItem
    (
        InvoiceItemId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceItem PRIMARY KEY,
        InvoiceId       BIGINT         NOT NULL,
        ChargeCode      VARCHAR(30)    NOT NULL,
        Description     NVARCHAR(200)  NOT NULL,
        Quantity        DECIMAL(10,2)  NOT NULL,
        UnitRate        DECIMAL(12,2)  NOT NULL,
        Amount          DECIMAL(14,2)  NOT NULL,
        IsAdjustment    BIT            NOT NULL CONSTRAINT DF_InvoiceItem_IsAdjustment DEFAULT (0),
        SortOrder       INT            NOT NULL CONSTRAINT DF_InvoiceItem_SortOrder DEFAULT (0),
        CreatedBy       BIGINT         NULL,
        CreatedDateUtc  DATETIME2(3)   NOT NULL CONSTRAINT DF_InvoiceItem_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'fin.Payment', N'U') IS NULL
BEGIN
    CREATE TABLE fin.Payment
    (
        PaymentId             BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
        PaymentNumber         VARCHAR(30)    NOT NULL CONSTRAINT UQ_Payment_PaymentNumber UNIQUE,
        InvoiceId             BIGINT         NOT NULL,
        BookingId             BIGINT         NOT NULL,
        CustomerId            BIGINT         NOT NULL,
        Amount                DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Payment_Amount CHECK (Amount > 0),
        RefundedAmount        DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Payment_RefundedAmount DEFAULT (0),
        PaymentMethodId       INT            NOT NULL,
        PaymentStatusId       INT            NOT NULL,
        GatewayName           VARCHAR(50)    NOT NULL,
        GatewayOrderId        VARCHAR(100)   NULL,
        GatewayTransactionId  VARCHAR(100)   NULL,
        ReferenceNumber       VARCHAR(100)   NULL,
        IdempotencyKey        VARCHAR(100)   NULL,
        PaymentDateUtc        DATETIME2(3)   NULL,
        FailureReason         NVARCHAR(500)  NULL,
        Remarks               NVARCHAR(500)  NULL,
        CreatedBy             BIGINT         NULL,
        CreatedDateUtc        DATETIME2(3)   NOT NULL CONSTRAINT DF_Payment_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy            BIGINT         NULL,
        ModifiedDateUtc       DATETIME2(3)   NULL,
        RowVersion            ROWVERSION     NOT NULL,
        CONSTRAINT CK_Payment_RefundedAmount CHECK (RefundedAmount >= 0 AND RefundedAmount <= Amount)
    );
END
GO

IF OBJECT_ID(N'fin.PaymentAttempt', N'U') IS NULL
BEGIN
    CREATE TABLE fin.PaymentAttempt
    (
        PaymentAttemptId  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentAttempt PRIMARY KEY,
        PaymentId         BIGINT         NOT NULL,
        AttemptNo         INT            NOT NULL,
        GatewayEventId    VARCHAR(100)   NULL,
        Status            VARCHAR(30)    NOT NULL,
        ResponseCode      VARCHAR(50)    NULL,
        ResponseMessage   NVARCHAR(500)  NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_PaymentAttempt_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_PaymentAttempt_Payment_AttemptNo UNIQUE (PaymentId, AttemptNo)
    );
END
GO

IF OBJECT_ID(N'fin.PaymentRefund', N'U') IS NULL
BEGIN
    CREATE TABLE fin.PaymentRefund
    (
        PaymentRefundId    BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentRefund PRIMARY KEY,
        RefundNumber       VARCHAR(30)    NOT NULL CONSTRAINT UQ_PaymentRefund_RefundNumber UNIQUE,
        PaymentId          BIGINT         NOT NULL,
        Amount             DECIMAL(14,2)  NOT NULL CONSTRAINT CK_PaymentRefund_Amount CHECK (Amount > 0),
        Reason             NVARCHAR(500)  NOT NULL,
        RefundStatusId     INT            NOT NULL,
        GatewayRefundId    VARCHAR(100)   NULL,
        ProcessedDateUtc   DATETIME2(3)   NULL,
        FailureReason      NVARCHAR(500)  NULL,
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_PaymentRefund_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'fin.Settlement', N'U') IS NULL
BEGIN
    CREATE TABLE fin.Settlement
    (
        SettlementId          BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Settlement PRIMARY KEY,
        SettlementNumber      VARCHAR(30)    NOT NULL CONSTRAINT UQ_Settlement_SettlementNumber UNIQUE,
        TripId                BIGINT         NOT NULL,
        OwnerId               BIGINT         NOT NULL,
        OwnerBankAccountId    BIGINT         NULL,
        GrossAmount           DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Settlement_GrossAmount CHECK (GrossAmount >= 0),
        CommissionPercent     DECIMAL(5,2)   NOT NULL,
        CommissionAmount      DECIMAL(14,2)  NOT NULL CONSTRAINT CK_Settlement_CommissionAmount CHECK (CommissionAmount >= 0),
        TaxAmount             DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Settlement_TaxAmount DEFAULT (0),
        AdjustmentAmount      DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Settlement_AdjustmentAmount DEFAULT (0),
        NetAmount             DECIMAL(14,2)  NOT NULL,
        SettlementStatusId    INT            NOT NULL,
        ApprovedBy            BIGINT         NULL,
        ApprovedDateUtc       DATETIME2(3)   NULL,
        SettlementDateUtc     DATETIME2(3)   NULL,
        TransactionReference  VARCHAR(100)   NULL,
        FailureReason         NVARCHAR(500)  NULL,
        CreatedBy             BIGINT         NULL,
        CreatedDateUtc        DATETIME2(3)   NOT NULL CONSTRAINT DF_Settlement_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy            BIGINT         NULL,
        ModifiedDateUtc       DATETIME2(3)   NULL,
        RowVersion            ROWVERSION     NOT NULL,
        CONSTRAINT CK_Settlement_NetAmount CHECK (NetAmount >= 0)
    );
END
GO

IF OBJECT_ID(N'fin.SettlementItem', N'U') IS NULL
BEGIN
    CREATE TABLE fin.SettlementItem
    (
        SettlementItemId  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_SettlementItem PRIMARY KEY,
        SettlementId      BIGINT         NOT NULL,
        ItemType          VARCHAR(30)    NOT NULL CONSTRAINT CK_SettlementItem_ItemType CHECK (ItemType IN ('Freight','Commission','Tax','Adjustment')),
        Description       NVARCHAR(200)  NOT NULL,
        Amount            DECIMAL(14,2)  NOT NULL,
        SortOrder         INT            NOT NULL CONSTRAINT DF_SettlementItem_SortOrder DEFAULT (0),
        CreatedBy         BIGINT         NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_SettlementItem_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
