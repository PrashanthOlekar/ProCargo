/*
    Booking and quotation tables.
    A booking is normalised into header, addresses (pickup/delivery), contacts (pickup/delivery),
    items, notes and status history instead of one oversized table.
*/

IF OBJECT_ID(N'core.Booking', N'U') IS NULL
BEGIN
    CREATE TABLE core.Booking
    (
        BookingId               BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Booking PRIMARY KEY,
        BookingNumber           VARCHAR(30)    NOT NULL CONSTRAINT UQ_Booking_BookingNumber UNIQUE,
        CustomerId              BIGINT         NOT NULL,
        VehicleTypeId           INT            NOT NULL,
        GoodsTypeId             INT            NOT NULL,
        GoodsDescription        NVARCHAR(500)  NOT NULL,
        TotalWeightKg           DECIMAL(10,2)  NOT NULL CONSTRAINT CK_Booking_TotalWeightKg CHECK (TotalWeightKg > 0),
        TotalQuantity           INT            NOT NULL CONSTRAINT CK_Booking_TotalQuantity CHECK (TotalQuantity > 0),
        RequestedPickupDateUtc  DATETIME2(3)   NOT NULL,
        SpecialInstructions     NVARCHAR(1000) NULL,
        EstimatedDistanceKm     DECIMAL(8,2)   NULL CONSTRAINT CK_Booking_EstimatedDistanceKm CHECK (EstimatedDistanceKm IS NULL OR EstimatedDistanceKm > 0),
        BookingStatusId         INT            NOT NULL,
        StatusBeforeHoldId      INT            NULL,
        CancellationReason      NVARCHAR(500)  NULL,
        CreatedBy               BIGINT         NULL,
        CreatedDateUtc          DATETIME2(3)   NOT NULL CONSTRAINT DF_Booking_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy              BIGINT         NULL,
        ModifiedDateUtc         DATETIME2(3)   NULL,
        RowVersion              ROWVERSION     NOT NULL
    );
END
GO

IF OBJECT_ID(N'core.BookingAddress', N'U') IS NULL
BEGIN
    CREATE TABLE core.BookingAddress
    (
        BookingAddressId  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingAddress PRIMARY KEY,
        BookingId         BIGINT         NOT NULL,
        AddressTypeId     INT            NOT NULL CONSTRAINT CK_BookingAddress_AddressTypeId CHECK (AddressTypeId IN (1, 2)), -- 1 Pickup, 2 Delivery
        AddressLine1      NVARCHAR(200)  NOT NULL,
        AddressLine2      NVARCHAR(200)  NULL,
        Landmark          NVARCHAR(150)  NULL,
        CityId            INT            NOT NULL,
        Pincode           CHAR(6)        NOT NULL CONSTRAINT CK_BookingAddress_Pincode CHECK (Pincode LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]'),
        Latitude          DECIMAL(9,6)   NULL CONSTRAINT CK_BookingAddress_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        Longitude         DECIMAL(9,6)   NULL CONSTRAINT CK_BookingAddress_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
        CONSTRAINT UQ_BookingAddress_Booking_Type UNIQUE (BookingId, AddressTypeId)
    );
END
GO

IF OBJECT_ID(N'core.BookingContact', N'U') IS NULL
BEGIN
    CREATE TABLE core.BookingContact
    (
        BookingContactId      BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingContact PRIMARY KEY,
        BookingId             BIGINT         NOT NULL,
        ContactTypeId         INT            NOT NULL CONSTRAINT CK_BookingContact_ContactTypeId CHECK (ContactTypeId IN (1, 2)), -- 1 Pickup, 2 Delivery
        ContactName           NVARCHAR(150)  NOT NULL,
        PhoneNumber           VARCHAR(20)    NOT NULL,
        AlternatePhoneNumber  VARCHAR(20)    NULL,
        CONSTRAINT UQ_BookingContact_Booking_Type UNIQUE (BookingId, ContactTypeId)
    );
END
GO

IF OBJECT_ID(N'core.BookingItem', N'U') IS NULL
BEGIN
    CREATE TABLE core.BookingItem
    (
        BookingItemId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingItem PRIMARY KEY,
        BookingId       BIGINT         NOT NULL,
        Description     NVARCHAR(200)  NOT NULL,
        Quantity        INT            NOT NULL CONSTRAINT CK_BookingItem_Quantity CHECK (Quantity > 0),
        WeightKg        DECIMAL(10,2)  NOT NULL CONSTRAINT CK_BookingItem_WeightKg CHECK (WeightKg > 0),
        LengthCm        DECIMAL(8,2)   NULL,
        WidthCm         DECIMAL(8,2)   NULL,
        HeightCm        DECIMAL(8,2)   NULL,
        IsFragile       BIT            NOT NULL CONSTRAINT DF_BookingItem_IsFragile DEFAULT (0),
        DeclaredValue   DECIMAL(14,2)  NULL CONSTRAINT CK_BookingItem_DeclaredValue CHECK (DeclaredValue IS NULL OR DeclaredValue >= 0)
    );
END
GO

IF OBJECT_ID(N'core.BookingNote', N'U') IS NULL
BEGIN
    CREATE TABLE core.BookingNote
    (
        BookingNoteId   BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingNote PRIMARY KEY,
        BookingId       BIGINT          NOT NULL,
        Note            NVARCHAR(1000)  NOT NULL,
        IsInternal      BIT             NOT NULL CONSTRAINT DF_BookingNote_IsInternal DEFAULT (1),
        CreatedBy       BIGINT          NOT NULL,
        CreatedDateUtc  DATETIME2(3)    NOT NULL CONSTRAINT DF_BookingNote_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.BookingStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.BookingStatusHistory
    (
        BookingStatusHistoryId BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingStatusHistory PRIMARY KEY,
        BookingId              BIGINT         NOT NULL,
        FromStatusId           INT            NULL,
        ToStatusId             INT            NOT NULL,
        Remarks                NVARCHAR(500)  NULL,
        ChangedBy              BIGINT         NULL,
        ChangedDateUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_BookingStatusHistory_ChangedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.Quotation', N'U') IS NULL
BEGIN
    CREATE TABLE core.Quotation
    (
        QuotationId            BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Quotation PRIMARY KEY,
        QuotationNumber        VARCHAR(30)    NOT NULL CONSTRAINT UQ_Quotation_QuotationNumber UNIQUE,
        BookingId              BIGINT         NOT NULL,
        VersionNo              INT            NOT NULL CONSTRAINT CK_Quotation_VersionNo CHECK (VersionNo > 0),
        DistanceKm             DECIMAL(8,2)   NOT NULL CONSTRAINT CK_Quotation_DistanceKm CHECK (DistanceKm > 0),
        BaseAmount             DECIMAL(14,2)  NOT NULL,
        DistanceCharge         DECIMAL(14,2)  NOT NULL,
        LoadingCharge          DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_LoadingCharge DEFAULT (0),
        UnloadingCharge        DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_UnloadingCharge DEFAULT (0),
        WaitingCharge          DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_WaitingCharge DEFAULT (0),
        TollCharge             DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_TollCharge DEFAULT (0),
        NightCharge            DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_NightCharge DEFAULT (0),
        SpecialHandlingCharge  DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_SpecialHandlingCharge DEFAULT (0),
        AdjustmentAmount       DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_AdjustmentAmount DEFAULT (0),
        DiscountAmount         DECIMAL(14,2)  NOT NULL CONSTRAINT DF_Quotation_DiscountAmount DEFAULT (0),
        SubTotal               DECIMAL(14,2)  NOT NULL,
        TaxPercent             DECIMAL(5,2)   NOT NULL,
        TaxAmount              DECIMAL(14,2)  NOT NULL,
        TotalAmount            DECIMAL(14,2)  NOT NULL,
        ValidityDateUtc        DATETIME2(3)   NOT NULL,
        QuotationStatusId      INT            NOT NULL,
        Notes                  NVARCHAR(1000) NULL,
        RejectionReason        NVARCHAR(500)  NULL,
        SentDateUtc            DATETIME2(3)   NULL,
        RespondedDateUtc       DATETIME2(3)   NULL,
        CreatedBy              BIGINT         NULL,
        CreatedDateUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_Quotation_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy             BIGINT         NULL,
        ModifiedDateUtc        DATETIME2(3)   NULL,
        RowVersion             ROWVERSION     NOT NULL,
        CONSTRAINT CK_Quotation_Amounts CHECK (DiscountAmount >= 0 AND TaxAmount >= 0 AND SubTotal >= 0 AND TotalAmount >= 0),
        CONSTRAINT CK_Quotation_TaxPercent CHECK (TaxPercent BETWEEN 0 AND 100),
        CONSTRAINT UQ_Quotation_Booking_Version UNIQUE (BookingId, VersionNo)
    );
END
GO

IF OBJECT_ID(N'core.QuotationCharge', N'U') IS NULL
BEGIN
    CREATE TABLE core.QuotationCharge
    (
        QuotationChargeId  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_QuotationCharge PRIMARY KEY,
        QuotationId        BIGINT         NOT NULL,
        ChargeCode         VARCHAR(30)    NOT NULL,
        Description        NVARCHAR(200)  NOT NULL,
        Quantity           DECIMAL(10,2)  NOT NULL,
        UnitRate           DECIMAL(12,2)  NOT NULL,
        Amount             DECIMAL(14,2)  NOT NULL,
        SortOrder          INT            NOT NULL CONSTRAINT DF_QuotationCharge_SortOrder DEFAULT (0)
    );
END
GO

IF OBJECT_ID(N'core.QuotationStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.QuotationStatusHistory
    (
        QuotationStatusHistoryId BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_QuotationStatusHistory PRIMARY KEY,
        QuotationId              BIGINT        NOT NULL,
        FromStatusId             INT           NULL,
        ToStatusId               INT           NOT NULL,
        Remarks                  NVARCHAR(500) NULL,
        ChangedBy                BIGINT        NULL,
        ChangedDateUtc           DATETIME2(3)  NOT NULL CONSTRAINT DF_QuotationStatusHistory_ChangedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
