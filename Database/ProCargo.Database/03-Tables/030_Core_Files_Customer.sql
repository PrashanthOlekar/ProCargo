/*
    File metadata and customer tables.
    Binary content is never stored in SQL Server - only metadata. Files live in Azure Blob Storage
    (or local disk in development) addressed by StorageKey.
*/

IF OBJECT_ID(N'core.StoredFile', N'U') IS NULL
BEGIN
    CREATE TABLE core.StoredFile
    (
        StoredFileId      BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_StoredFile PRIMARY KEY,
        StorageKey        VARCHAR(400)   NOT NULL CONSTRAINT UQ_StoredFile_StorageKey UNIQUE,
        OriginalFileName  NVARCHAR(255)  NOT NULL,
        ContentType       VARCHAR(100)   NOT NULL,
        SizeBytes         BIGINT         NOT NULL CONSTRAINT CK_StoredFile_SizeBytes CHECK (SizeBytes > 0),
        Sha256Hash        BINARY(32)     NOT NULL,
        UploadedBy        BIGINT         NOT NULL,
        IsDeleted         BIT            NOT NULL CONSTRAINT DF_StoredFile_IsDeleted DEFAULT (0),
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_StoredFile_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.Customer', N'U') IS NULL
BEGIN
    CREATE TABLE core.Customer
    (
        CustomerId        BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
        UserId            BIGINT         NOT NULL CONSTRAINT UQ_Customer_UserId UNIQUE,
        CustomerNumber    VARCHAR(30)    NOT NULL CONSTRAINT UQ_Customer_CustomerNumber UNIQUE,
        CustomerTypeId    INT            NOT NULL,
        FullName          NVARCHAR(150)  NOT NULL,
        CompanyName       NVARCHAR(200)  NULL,
        Email             NVARCHAR(256)  NOT NULL,
        PhoneNumber       VARCHAR(20)    NOT NULL,
        GstNumber         VARCHAR(15)    NULL,
        IsActive          BIT            NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1),
        IsDeleted         BIT            NOT NULL CONSTRAINT DF_Customer_IsDeleted DEFAULT (0),
        CreatedBy         BIGINT         NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_Customer_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy        BIGINT         NULL,
        ModifiedDateUtc   DATETIME2(3)   NULL,
        RowVersion        ROWVERSION     NOT NULL,
        CONSTRAINT CK_Customer_GstNumber CHECK (GstNumber IS NULL OR LEN(GstNumber) = 15),
        CONSTRAINT CK_Customer_CompanyName CHECK (CustomerTypeId <> 2 OR CompanyName IS NOT NULL)
    );
END
GO

IF OBJECT_ID(N'core.CustomerAddress', N'U') IS NULL
BEGIN
    CREATE TABLE core.CustomerAddress
    (
        CustomerAddressId BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerAddress PRIMARY KEY,
        CustomerId        BIGINT         NOT NULL,
        Label             NVARCHAR(50)   NOT NULL,
        AddressLine1      NVARCHAR(200)  NOT NULL,
        AddressLine2      NVARCHAR(200)  NULL,
        Landmark          NVARCHAR(150)  NULL,
        CityId            INT            NOT NULL,
        Pincode           CHAR(6)        NOT NULL CONSTRAINT CK_CustomerAddress_Pincode CHECK (Pincode LIKE '[1-9][0-9][0-9][0-9][0-9][0-9]'),
        Latitude          DECIMAL(9,6)   NULL CONSTRAINT CK_CustomerAddress_Latitude CHECK (Latitude IS NULL OR Latitude BETWEEN -90 AND 90),
        Longitude         DECIMAL(9,6)   NULL CONSTRAINT CK_CustomerAddress_Longitude CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180),
        IsDefault         BIT            NOT NULL CONSTRAINT DF_CustomerAddress_IsDefault DEFAULT (0),
        IsDeleted         BIT            NOT NULL CONSTRAINT DF_CustomerAddress_IsDeleted DEFAULT (0),
        CreatedBy         BIGINT         NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_CustomerAddress_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy        BIGINT         NULL,
        ModifiedDateUtc   DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.CustomerContact', N'U') IS NULL
BEGIN
    CREATE TABLE core.CustomerContact
    (
        CustomerContactId BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerContact PRIMARY KEY,
        CustomerId        BIGINT         NOT NULL,
        ContactName       NVARCHAR(150)  NOT NULL,
        PhoneNumber       VARCHAR(20)    NOT NULL,
        Email             NVARCHAR(256)  NULL,
        Designation       NVARCHAR(100)  NULL,
        IsPrimary         BIT            NOT NULL CONSTRAINT DF_CustomerContact_IsPrimary DEFAULT (0),
        IsDeleted         BIT            NOT NULL CONSTRAINT DF_CustomerContact_IsDeleted DEFAULT (0),
        CreatedBy         BIGINT         NULL,
        CreatedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_CustomerContact_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy        BIGINT         NULL,
        ModifiedDateUtc   DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'core.CustomerDocument', N'U') IS NULL
BEGIN
    CREATE TABLE core.CustomerDocument
    (
        CustomerDocumentId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerDocument PRIMARY KEY,
        CustomerId           BIGINT         NOT NULL,
        DocumentTypeId       INT            NOT NULL,
        StoredFileId         BIGINT         NOT NULL,
        DocumentNumber       NVARCHAR(50)   NULL,
        ExpiryDate           DATE           NULL,
        VerificationStatusId INT            NOT NULL CONSTRAINT DF_CustomerDocument_VerificationStatusId DEFAULT (1),
        VerifiedBy           BIGINT         NULL,
        VerifiedDateUtc      DATETIME2(3)   NULL,
        Remarks              NVARCHAR(500)  NULL,
        IsDeleted            BIT            NOT NULL CONSTRAINT DF_CustomerDocument_IsDeleted DEFAULT (0),
        CreatedBy            BIGINT         NULL,
        CreatedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_CustomerDocument_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy           BIGINT         NULL,
        ModifiedDateUtc      DATETIME2(3)   NULL
    );
END
GO
