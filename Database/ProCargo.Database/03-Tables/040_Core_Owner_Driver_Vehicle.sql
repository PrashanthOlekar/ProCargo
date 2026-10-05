/*
    Vehicle owner, driver and vehicle tables.
    Sensitive values (PAN, bank account numbers) are stored encrypted by the API (ASP.NET Core Data Protection);
    only the last four characters are kept in clear text for display.
*/

IF OBJECT_ID(N'core.VehicleOwner', N'U') IS NULL
BEGIN
    CREATE TABLE core.VehicleOwner
    (
        OwnerId              BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleOwner PRIMARY KEY,
        UserId               BIGINT         NOT NULL CONSTRAINT UQ_VehicleOwner_UserId UNIQUE,
        OwnerNumber          VARCHAR(30)    NOT NULL CONSTRAINT UQ_VehicleOwner_OwnerNumber UNIQUE,
        OwnerTypeId          INT            NOT NULL,
        FullName             NVARCHAR(150)  NOT NULL,
        Email                NVARCHAR(256)  NOT NULL,
        PhoneNumber          VARCHAR(20)    NOT NULL,
        PanNumberEncrypted   NVARCHAR(500)  NULL,
        PanLast4             CHAR(4)        NULL,
        VerificationStatusId INT            NOT NULL CONSTRAINT DF_VehicleOwner_VerificationStatusId DEFAULT (1),
        VerificationRemarks  NVARCHAR(500)  NULL,
        VerifiedBy           BIGINT         NULL,
        VerifiedDateUtc      DATETIME2(3)   NULL,
        IsActive             BIT            NOT NULL CONSTRAINT DF_VehicleOwner_IsActive DEFAULT (1),
        IsDeleted            BIT            NOT NULL CONSTRAINT DF_VehicleOwner_IsDeleted DEFAULT (0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_VehicleOwner_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL,
        RowVersion           ROWVERSION     NOT NULL
    );
END
GO

IF OBJECT_ID(N'core.OwnerBusiness', N'U') IS NULL
BEGIN
    CREATE TABLE core.OwnerBusiness
    (
        OwnerBusinessId      BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_OwnerBusiness PRIMARY KEY,
        OwnerId              BIGINT         NOT NULL CONSTRAINT UQ_OwnerBusiness_OwnerId UNIQUE,
        BusinessName         NVARCHAR(200)  NOT NULL,
        GstNumber            VARCHAR(15)    NULL CONSTRAINT CK_OwnerBusiness_GstNumber CHECK (GstNumber IS NULL OR LEN(GstNumber) = 15),
        RegistrationNumber   NVARCHAR(50)   NULL,
        FleetSize            INT            NULL CONSTRAINT CK_OwnerBusiness_FleetSize CHECK (FleetSize IS NULL OR FleetSize >= 0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_OwnerBusiness_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.OwnerAddress', N'U') IS NULL
BEGIN
    CREATE TABLE core.OwnerAddress
    (
        OwnerAddressId    BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_OwnerAddress PRIMARY KEY,
        OwnerId           BIGINT         NOT NULL,
        AddressLine1      NVARCHAR(200)  NOT NULL,
        AddressLine2      NVARCHAR(200)  NULL,
        Landmark          NVARCHAR(150)  NULL,
        CityId            INT            NOT NULL,
        Pincode           CHAR(6)        NOT NULL CONSTRAINT CK_OwnerAddress_Pincode CHECK (Pincode LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]'),
        IsPrimary         BIT            NOT NULL CONSTRAINT DF_OwnerAddress_IsPrimary DEFAULT (0),
        IsDeleted         BIT            NOT NULL CONSTRAINT DF_OwnerAddress_IsDeleted DEFAULT (0),
        CreatedBy         BIGINT         NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_OwnerAddress_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy        BIGINT         NULL,
        ModifiedDateUtc   DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.OwnerBankAccount', N'U') IS NULL
BEGIN
    CREATE TABLE core.OwnerBankAccount
    (
        OwnerBankAccountId      BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_OwnerBankAccount PRIMARY KEY,
        OwnerId                 BIGINT         NOT NULL,
        AccountHolderName       NVARCHAR(150)  NOT NULL,
        BankName                NVARCHAR(150)  NOT NULL,
        AccountNumberEncrypted  NVARCHAR(500)  NOT NULL,
        AccountNumberLast4      CHAR(4)        NOT NULL,
        IfscCode                CHAR(11)       NOT NULL CONSTRAINT CK_OwnerBankAccount_IfscCode CHECK (IfscCode LIKE '[A-Z][A-Z][A-Z][A-Z]0[A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9]'),
        IsPrimary               BIT            NOT NULL CONSTRAINT DF_OwnerBankAccount_IsPrimary DEFAULT (0),
        VerificationStatusId    INT            NOT NULL CONSTRAINT DF_OwnerBankAccount_VerificationStatusId DEFAULT (1),
        IsActive                BIT            NOT NULL CONSTRAINT DF_OwnerBankAccount_IsActive DEFAULT (1),
        CreatedBy               BIGINT         NULL,
        CreatedDateUtc          DATETIME2(3)   NOT NULL CONSTRAINT DF_OwnerBankAccount_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy              BIGINT         NULL,
        ModifiedDateUtc         DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.OwnerDocument', N'U') IS NULL
BEGIN
    CREATE TABLE core.OwnerDocument
    (
        OwnerDocumentId      BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_OwnerDocument PRIMARY KEY,
        OwnerId              BIGINT         NOT NULL,
        DocumentTypeId       INT            NOT NULL,
        StoredFileId         BIGINT         NOT NULL,
        DocumentNumber       NVARCHAR(50)   NULL,
        ExpiryDate           DATE           NULL,
        VerificationStatusId INT            NOT NULL CONSTRAINT DF_OwnerDocument_VerificationStatusId DEFAULT (1),
        VerifiedBy           BIGINT         NULL,
        VerifiedDateUtc      DATETIME2(3)   NULL,
        Remarks              NVARCHAR(500)  NULL,
        IsDeleted            BIT            NOT NULL CONSTRAINT DF_OwnerDocument_IsDeleted DEFAULT (0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_OwnerDocument_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.Driver', N'U') IS NULL
BEGIN
    CREATE TABLE core.Driver
    (
        DriverId               BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Driver PRIMARY KEY,
        UserId                 BIGINT         NOT NULL CONSTRAINT UQ_Driver_UserId UNIQUE,
        DriverNumber           VARCHAR(30)    NOT NULL CONSTRAINT UQ_Driver_DriverNumber UNIQUE,
        OwnerId                BIGINT         NULL,
        FullName               NVARCHAR(150)  NOT NULL,
        PhoneNumber            VARCHAR(20)    NOT NULL,
        AlternatePhoneNumber   VARCHAR(20)    NULL,
        DateOfBirth            DATE           NULL,
        VerificationStatusId   INT            NOT NULL CONSTRAINT DF_Driver_VerificationStatusId DEFAULT (1),
        VerificationRemarks    NVARCHAR(500)  NULL,
        VerifiedBy             BIGINT         NULL,
        VerifiedDateUtc        DATETIME2(3)   NULL,
        AvailabilityStatusId   INT            NOT NULL CONSTRAINT DF_Driver_AvailabilityStatusId DEFAULT (3),
        IsActive               BIT            NOT NULL CONSTRAINT DF_Driver_IsActive DEFAULT (1),
        IsDeleted              BIT            NOT NULL CONSTRAINT DF_Driver_IsDeleted DEFAULT (0),
        CreatedBy              BIGINT         NULL,
        CreatedDateUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_Driver_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy             BIGINT         NULL,
        ModifiedDateUtc        DATETIME2(3)   NULL,
        RowVersion             ROWVERSION     NOT NULL
    );
END
GO

IF OBJECT_ID(N'core.DriverLicense', N'U') IS NULL
BEGIN
    CREATE TABLE core.DriverLicense
    (
        DriverLicenseId    BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverLicense PRIMARY KEY,
        DriverId           BIGINT         NOT NULL,
        LicenseNumber      VARCHAR(20)    NOT NULL,
        LicenseClass       VARCHAR(50)    NOT NULL,
        IssueDate          DATE           NULL,
        ExpiryDate         DATE           NOT NULL,
        IssuingAuthority   NVARCHAR(100)  NULL,
        IsCurrent          BIT            NOT NULL CONSTRAINT DF_DriverLicense_IsCurrent DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_DriverLicense_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL,
        CONSTRAINT CK_DriverLicense_Dates CHECK (IssueDate IS NULL OR ExpiryDate > IssueDate)
    );
END
GO

IF OBJECT_ID(N'core.DriverDocument', N'U') IS NULL
BEGIN
    CREATE TABLE core.DriverDocument
    (
        DriverDocumentId     BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverDocument PRIMARY KEY,
        DriverId             BIGINT         NOT NULL,
        DocumentTypeId       INT            NOT NULL,
        StoredFileId         BIGINT         NOT NULL,
        DocumentNumber       NVARCHAR(50)   NULL,
        ExpiryDate           DATE           NULL,
        VerificationStatusId INT            NOT NULL CONSTRAINT DF_DriverDocument_VerificationStatusId DEFAULT (1),
        VerifiedBy           BIGINT         NULL,
        VerifiedDateUtc      DATETIME2(3)   NULL,
        Remarks              NVARCHAR(500)  NULL,
        IsDeleted            BIT            NOT NULL CONSTRAINT DF_DriverDocument_IsDeleted DEFAULT (0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_DriverDocument_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.DriverAvailabilityHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.DriverAvailabilityHistory
    (
        DriverAvailabilityHistoryId BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_DriverAvailabilityHistory PRIMARY KEY,
        DriverId                    BIGINT        NOT NULL,
        AvailabilityStatusId        INT           NOT NULL,
        Reason                      NVARCHAR(300) NULL,
        ChangedBy                   BIGINT        NULL,
        ChangedDateUtc              DATETIME2(3)  NOT NULL CONSTRAINT DF_DriverAvailabilityHistory_ChangedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.Vehicle', N'U') IS NULL
BEGIN
    CREATE TABLE core.Vehicle
    (
        VehicleId              BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicle PRIMARY KEY,
        OwnerId                BIGINT         NOT NULL,
        VehicleNumber          VARCHAR(15)    NOT NULL CONSTRAINT UQ_Vehicle_VehicleNumber UNIQUE,
        VehicleTypeId          INT            NOT NULL,
        Manufacturer           NVARCHAR(100)  NOT NULL,
        Model                  NVARCHAR(100)  NOT NULL,
        ManufactureYear        INT            NULL CONSTRAINT CK_Vehicle_ManufactureYear CHECK (ManufactureYear IS NULL OR ManufactureYear BETWEEN 1980 AND 2100),
        CapacityKg             DECIMAL(10,2)  NOT NULL CONSTRAINT CK_Vehicle_CapacityKg CHECK (CapacityKg > 0),
        PermitNumber           VARCHAR(50)    NULL,
        PermitExpiryDate       DATE           NULL,
        InsuranceNumber        VARCHAR(50)    NULL,
        InsuranceExpiryDate    DATE           NULL,
        FitnessExpiryDate      DATE           NULL,
        PucExpiryDate          DATE           NULL,
        VerificationStatusId   INT            NOT NULL CONSTRAINT DF_Vehicle_VerificationStatusId DEFAULT (1),
        VerificationRemarks    NVARCHAR(500)  NULL,
        VerifiedBy             BIGINT         NULL,
        VerifiedDateUtc        DATETIME2(3)   NULL,
        IsAvailable            BIT            NOT NULL CONSTRAINT DF_Vehicle_IsAvailable DEFAULT (0),
        IsActive               BIT            NOT NULL CONSTRAINT DF_Vehicle_IsActive DEFAULT (1),
        IsDeleted              BIT            NOT NULL CONSTRAINT DF_Vehicle_IsDeleted DEFAULT (0),
        CreatedBy              BIGINT         NULL,
        CreatedDateUtc         DATETIME2(3)   NOT NULL CONSTRAINT DF_Vehicle_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy             BIGINT         NULL,
        ModifiedDateUtc        DATETIME2(3)   NULL,
        RowVersion             ROWVERSION     NOT NULL,
        CONSTRAINT CK_Vehicle_VehicleNumber CHECK (VehicleNumber NOT LIKE '%[^A-Z0-9]%' AND LEN(VehicleNumber) BETWEEN 6 AND 15)
    );
END
GO

IF OBJECT_ID(N'core.VehicleDocument', N'U') IS NULL
BEGIN
    CREATE TABLE core.VehicleDocument
    (
        VehicleDocumentId    BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleDocument PRIMARY KEY,
        VehicleId            BIGINT         NOT NULL,
        DocumentTypeId       INT            NOT NULL,
        StoredFileId         BIGINT         NOT NULL,
        DocumentNumber       NVARCHAR(50)   NULL,
        ExpiryDate           DATE           NULL,
        VerificationStatusId INT            NOT NULL CONSTRAINT DF_VehicleDocument_VerificationStatusId DEFAULT (1),
        VerifiedBy           BIGINT         NULL,
        VerifiedDateUtc      DATETIME2(3)   NULL,
        Remarks              NVARCHAR(500)  NULL,
        IsDeleted            BIT            NOT NULL CONSTRAINT DF_VehicleDocument_IsDeleted DEFAULT (0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_VehicleDocument_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.VehicleAvailabilityHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.VehicleAvailabilityHistory
    (
        VehicleAvailabilityHistoryId BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleAvailabilityHistory PRIMARY KEY,
        VehicleId                    BIGINT        NOT NULL,
        IsAvailable                  BIT           NOT NULL,
        Reason                       NVARCHAR(300) NULL,
        ChangedBy                    BIGINT        NULL,
        ChangedDateUtc               DATETIME2(3)  NOT NULL CONSTRAINT DF_VehicleAvailabilityHistory_ChangedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
