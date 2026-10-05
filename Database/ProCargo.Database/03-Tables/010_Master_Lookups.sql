/*
    System-defined lookup tables (status/type codes).
    Ids are fixed (no IDENTITY) because application code references them through enums.
    Values are seeded in 09-SeedData and must not be edited through the application.
*/

-- Booking lifecycle states. Ids are mirrored by ProCargo.Domain.Enums.BookingStatus.
IF OBJECT_ID(N'mst.BookingStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.BookingStatus
    (
        BookingStatusId   INT            NOT NULL CONSTRAINT PK_BookingStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_BookingStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_BookingStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_BookingStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Trip lifecycle states. Mirrored by Domain.Enums.TripStatus.
IF OBJECT_ID(N'mst.TripStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.TripStatus
    (
        TripStatusId   INT            NOT NULL CONSTRAINT PK_TripStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_TripStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_TripStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_TripStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Quotation lifecycle states.
IF OBJECT_ID(N'mst.QuotationStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.QuotationStatus
    (
        QuotationStatusId   INT            NOT NULL CONSTRAINT PK_QuotationStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_QuotationStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_QuotationStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_QuotationStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Payment states.
IF OBJECT_ID(N'mst.PaymentStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.PaymentStatus
    (
        PaymentStatusId   INT            NOT NULL CONSTRAINT PK_PaymentStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_PaymentStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_PaymentStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_PaymentStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Invoice states.
IF OBJECT_ID(N'mst.InvoiceStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.InvoiceStatus
    (
        InvoiceStatusId   INT            NOT NULL CONSTRAINT PK_InvoiceStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_InvoiceStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_InvoiceStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_InvoiceStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Owner settlement states.
IF OBJECT_ID(N'mst.SettlementStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.SettlementStatus
    (
        SettlementStatusId   INT            NOT NULL CONSTRAINT PK_SettlementStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_SettlementStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_SettlementStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_SettlementStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Refund states.
IF OBJECT_ID(N'mst.RefundStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.RefundStatus
    (
        RefundStatusId   INT            NOT NULL CONSTRAINT PK_RefundStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_RefundStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_RefundStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_RefundStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- KYC / document / vehicle verification states.
IF OBJECT_ID(N'mst.VerificationStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.VerificationStatus
    (
        VerificationStatusId   INT            NOT NULL CONSTRAINT PK_VerificationStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_VerificationStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_VerificationStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_VerificationStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Driver availability states.
IF OBJECT_ID(N'mst.DriverAvailabilityStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.DriverAvailabilityStatus
    (
        DriverAvailabilityStatusId   INT            NOT NULL CONSTRAINT PK_DriverAvailabilityStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_DriverAvailabilityStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_DriverAvailabilityStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_DriverAvailabilityStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Support ticket states.
IF OBJECT_ID(N'mst.TicketStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.TicketStatus
    (
        TicketStatusId   INT            NOT NULL CONSTRAINT PK_TicketStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_TicketStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_TicketStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_TicketStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Support ticket priorities.
IF OBJECT_ID(N'mst.TicketPriority', N'U') IS NULL
BEGIN
    CREATE TABLE mst.TicketPriority
    (
        TicketPriorityId   INT            NOT NULL CONSTRAINT PK_TicketPriority PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_TicketPriority_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_TicketPriority_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_TicketPriority_IsTerminal DEFAULT (0)
    );
END
GO

-- Complaint states.
IF OBJECT_ID(N'mst.ComplaintStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.ComplaintStatus
    (
        ComplaintStatusId   INT            NOT NULL CONSTRAINT PK_ComplaintStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_ComplaintStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_ComplaintStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_ComplaintStatus_IsTerminal DEFAULT (0)
    );
END
GO

-- Individual or business customer.
IF OBJECT_ID(N'mst.CustomerType', N'U') IS NULL
BEGIN
    CREATE TABLE mst.CustomerType
    (
        CustomerTypeId   INT            NOT NULL CONSTRAINT PK_CustomerType PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_CustomerType_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_CustomerType_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_CustomerType_IsTerminal DEFAULT (0)
    );
END
GO

-- Individual owner or fleet business.
IF OBJECT_ID(N'mst.OwnerType', N'U') IS NULL
BEGIN
    CREATE TABLE mst.OwnerType
    (
        OwnerTypeId   INT            NOT NULL CONSTRAINT PK_OwnerType PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_OwnerType_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_OwnerType_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_OwnerType_IsTerminal DEFAULT (0)
    );
END
GO

-- How a payment was made.
IF OBJECT_ID(N'mst.PaymentMethod', N'U') IS NULL
BEGIN
    CREATE TABLE mst.PaymentMethod
    (
        PaymentMethodId   INT            NOT NULL CONSTRAINT PK_PaymentMethod PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_PaymentMethod_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_PaymentMethod_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_PaymentMethod_IsTerminal DEFAULT (0)
    );
END
GO

-- Delivery channel of a notification.
IF OBJECT_ID(N'mst.NotificationChannel', N'U') IS NULL
BEGIN
    CREATE TABLE mst.NotificationChannel
    (
        NotificationChannelId   INT            NOT NULL CONSTRAINT PK_NotificationChannel PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_NotificationChannel_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_NotificationChannel_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_NotificationChannel_IsTerminal DEFAULT (0)
    );
END
GO

-- Delivery state of a notification.
IF OBJECT_ID(N'mst.NotificationStatus', N'U') IS NULL
BEGIN
    CREATE TABLE mst.NotificationStatus
    (
        NotificationStatusId   INT            NOT NULL CONSTRAINT PK_NotificationStatus PRIMARY KEY,
        Code       VARCHAR(40)    NOT NULL CONSTRAINT UQ_NotificationStatus_Code UNIQUE,
        Name       NVARCHAR(80)   NOT NULL,
        SortOrder  INT            NOT NULL CONSTRAINT DF_NotificationStatus_SortOrder DEFAULT (0),
        IsTerminal BIT            NOT NULL CONSTRAINT DF_NotificationStatus_IsTerminal DEFAULT (0)
    );
END
GO
