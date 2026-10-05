/*
    Editable reference data: geography, vehicle types, goods types, document types, system settings.
    Managed by users holding the ManageMasterData / ManageSystemSettings permissions.
*/

IF OBJECT_ID(N'mst.State', N'U') IS NULL
BEGIN
    CREATE TABLE mst.State
    (
        StateId          INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_State PRIMARY KEY,
        StateCode        CHAR(2)        NOT NULL CONSTRAINT UQ_State_StateCode UNIQUE,
        Name             NVARCHAR(100)  NOT NULL CONSTRAINT UQ_State_Name UNIQUE,
        IsActive         BIT            NOT NULL CONSTRAINT DF_State_IsActive DEFAULT (1),
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_State_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'mst.City', N'U') IS NULL
BEGIN
    CREATE TABLE mst.City
    (
        CityId           INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_City PRIMARY KEY,
        StateId          INT            NOT NULL,
        Name             NVARCHAR(100)  NOT NULL,
        IsActive         BIT            NOT NULL CONSTRAINT DF_City_IsActive DEFAULT (1),
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_City_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL,
        CONSTRAINT UQ_City_State_Name UNIQUE (StateId, Name)
    );
END
GO

IF OBJECT_ID(N'mst.VehicleType', N'U') IS NULL
BEGIN
    CREATE TABLE mst.VehicleType
    (
        VehicleTypeId    INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehicleType PRIMARY KEY,
        Code             VARCHAR(40)    NOT NULL CONSTRAINT UQ_VehicleType_Code UNIQUE,
        Name             NVARCHAR(100)  NOT NULL,
        Description      NVARCHAR(500)  NULL,
        CapacityKg       DECIMAL(10,2)  NOT NULL CONSTRAINT CK_VehicleType_CapacityKg CHECK (CapacityKg > 0),
        LengthFt         DECIMAL(5,2)   NULL CONSTRAINT CK_VehicleType_LengthFt CHECK (LengthFt IS NULL OR LengthFt > 0),
        SortOrder        INT            NOT NULL CONSTRAINT DF_VehicleType_SortOrder DEFAULT (0),
        IsActive         BIT            NOT NULL CONSTRAINT DF_VehicleType_IsActive DEFAULT (1),
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_VehicleType_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'mst.GoodsType', N'U') IS NULL
BEGIN
    CREATE TABLE mst.GoodsType
    (
        GoodsTypeId              INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_GoodsType PRIMARY KEY,
        Code                     VARCHAR(40)    NOT NULL CONSTRAINT UQ_GoodsType_Code UNIQUE,
        Name                     NVARCHAR(100)  NOT NULL,
        RequiresSpecialHandling  BIT            NOT NULL CONSTRAINT DF_GoodsType_RequiresSpecialHandling DEFAULT (0),
        IsActive                 BIT            NOT NULL CONSTRAINT DF_GoodsType_IsActive DEFAULT (1),
        CreatedBy                BIGINT         NULL,
        CreatedDateUtc           DATETIME2(3)   NOT NULL CONSTRAINT DF_GoodsType_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy               BIGINT         NULL,
        ModifiedDateUtc          DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'mst.DocumentType', N'U') IS NULL
BEGIN
    CREATE TABLE mst.DocumentType
    (
        DocumentTypeId   INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentType PRIMARY KEY,
        Code             VARCHAR(40)    NOT NULL CONSTRAINT UQ_DocumentType_Code UNIQUE,
        Name             NVARCHAR(100)  NOT NULL,
        AppliesTo        VARCHAR(20)    NOT NULL CONSTRAINT CK_DocumentType_AppliesTo CHECK (AppliesTo IN ('Customer','Owner','Driver','Vehicle')),
        RequiresExpiry   BIT            NOT NULL CONSTRAINT DF_DocumentType_RequiresExpiry DEFAULT (0),
        IsMandatory      BIT            NOT NULL CONSTRAINT DF_DocumentType_IsMandatory DEFAULT (0),
        IsActive         BIT            NOT NULL CONSTRAINT DF_DocumentType_IsActive DEFAULT (1),
        CreatedBy        BIGINT         NULL,
        CreatedDateUtc   DATETIME2(3)   NOT NULL CONSTRAINT DF_DocumentType_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'mst.SystemSetting', N'U') IS NULL
BEGIN
    CREATE TABLE mst.SystemSetting
    (
        SettingKey       VARCHAR(100)   NOT NULL CONSTRAINT PK_SystemSetting PRIMARY KEY,
        SettingValue     NVARCHAR(1000) NOT NULL,
        DataType         VARCHAR(20)    NOT NULL CONSTRAINT CK_SystemSetting_DataType CHECK (DataType IN ('String','Int','Decimal','Bool')),
        Description      NVARCHAR(300)  NULL,
        IsEditable       BIT            NOT NULL CONSTRAINT DF_SystemSetting_IsEditable DEFAULT (1),
        ModifiedBy       BIGINT         NULL,
        ModifiedDateUtc  DATETIME2(3)   NULL
    );
END
GO
