/*
    Module: Master data & system settings (schema mst)
*/

-- All system lookups in one round trip (dropdowns, status labels).
CREATE OR ALTER PROCEDURE mst.usp_Lookup_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 'BookingStatus' AS LookupType, BookingStatusId AS Id, Code, Name, SortOrder, IsTerminal FROM mst.BookingStatus
    UNION ALL SELECT 'TripStatus', TripStatusId, Code, Name, SortOrder, IsTerminal FROM mst.TripStatus
    UNION ALL SELECT 'QuotationStatus', QuotationStatusId, Code, Name, SortOrder, IsTerminal FROM mst.QuotationStatus
    UNION ALL SELECT 'PaymentStatus', PaymentStatusId, Code, Name, SortOrder, IsTerminal FROM mst.PaymentStatus
    UNION ALL SELECT 'InvoiceStatus', InvoiceStatusId, Code, Name, SortOrder, IsTerminal FROM mst.InvoiceStatus
    UNION ALL SELECT 'SettlementStatus', SettlementStatusId, Code, Name, SortOrder, IsTerminal FROM mst.SettlementStatus
    UNION ALL SELECT 'RefundStatus', RefundStatusId, Code, Name, SortOrder, IsTerminal FROM mst.RefundStatus
    UNION ALL SELECT 'VerificationStatus', VerificationStatusId, Code, Name, SortOrder, IsTerminal FROM mst.VerificationStatus
    UNION ALL SELECT 'DriverAvailabilityStatus', DriverAvailabilityStatusId, Code, Name, SortOrder, IsTerminal FROM mst.DriverAvailabilityStatus
    UNION ALL SELECT 'TicketStatus', TicketStatusId, Code, Name, SortOrder, IsTerminal FROM mst.TicketStatus
    UNION ALL SELECT 'TicketPriority', TicketPriorityId, Code, Name, SortOrder, IsTerminal FROM mst.TicketPriority
    UNION ALL SELECT 'ComplaintStatus', ComplaintStatusId, Code, Name, SortOrder, IsTerminal FROM mst.ComplaintStatus
    UNION ALL SELECT 'CustomerType', CustomerTypeId, Code, Name, SortOrder, IsTerminal FROM mst.CustomerType
    UNION ALL SELECT 'OwnerType', OwnerTypeId, Code, Name, SortOrder, IsTerminal FROM mst.OwnerType
    UNION ALL SELECT 'PaymentMethod', PaymentMethodId, Code, Name, SortOrder, IsTerminal FROM mst.PaymentMethod
    ORDER BY LookupType, SortOrder;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_State_GetAll
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.StateId, s.StateCode, s.Name, s.IsActive
    FROM mst.State AS s
    WHERE @IncludeInactive = 1 OR s.IsActive = 1
    ORDER BY s.Name;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_State_Save
    @StateId    INT = NULL,
    @StateCode  CHAR(2),
    @Name       NVARCHAR(100),
    @IsActive   BIT,
    @UserId     BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @StateId IS NULL
    BEGIN
        INSERT INTO mst.State (StateCode, Name, IsActive, CreatedBy) VALUES (UPPER(@StateCode), @Name, @IsActive, @UserId);
        SET @StateId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE mst.State
        SET StateCode = UPPER(@StateCode), Name = @Name, IsActive = @IsActive, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE StateId = @StateId;
        IF @@ROWCOUNT = 0 THROW 50404, N'STATE_NOT_FOUND|State was not found.', 1;
    END

    SELECT CAST(@StateId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_City_GetByState
    @StateId          INT = NULL,
    @IncludeInactive  BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CityId, c.StateId, s.Name AS StateName, c.Name, c.IsActive
    FROM mst.City AS c
    INNER JOIN mst.State AS s ON s.StateId = c.StateId
    WHERE (@StateId IS NULL OR c.StateId = @StateId)
      AND (@IncludeInactive = 1 OR (c.IsActive = 1 AND s.IsActive = 1))
    ORDER BY s.Name, c.Name;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_City_GetById
    @CityId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.CityId, c.StateId, s.Name AS StateName, c.Name, c.IsActive
    FROM mst.City AS c
    INNER JOIN mst.State AS s ON s.StateId = c.StateId
    WHERE c.CityId = @CityId;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_City_Save
    @CityId    INT = NULL,
    @StateId   INT,
    @Name      NVARCHAR(100),
    @IsActive  BIT,
    @UserId    BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM mst.City WHERE StateId = @StateId AND Name = @Name AND (@CityId IS NULL OR CityId <> @CityId))
        THROW 50409, N'CITY_EXISTS|A city with this name already exists in the state.', 1;

    IF @CityId IS NULL
    BEGIN
        INSERT INTO mst.City (StateId, Name, IsActive, CreatedBy) VALUES (@StateId, @Name, @IsActive, @UserId);
        SET @CityId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE mst.City
        SET StateId = @StateId, Name = @Name, IsActive = @IsActive, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE CityId = @CityId;
        IF @@ROWCOUNT = 0 THROW 50404, N'CITY_NOT_FOUND|City was not found.', 1;
    END

    SELECT CAST(@CityId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_VehicleType_GetAll
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT vt.VehicleTypeId, vt.Code, vt.Name, vt.Description, vt.CapacityKg, vt.LengthFt, vt.SortOrder, vt.IsActive
    FROM mst.VehicleType AS vt
    WHERE @IncludeInactive = 1 OR vt.IsActive = 1
    ORDER BY vt.SortOrder, vt.Name;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_VehicleType_Save
    @VehicleTypeId  INT = NULL,
    @Code           VARCHAR(40),
    @Name           NVARCHAR(100),
    @Description    NVARCHAR(500),
    @CapacityKg     DECIMAL(10,2),
    @LengthFt       DECIMAL(5,2),
    @SortOrder      INT,
    @IsActive       BIT,
    @UserId         BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM mst.VehicleType WHERE Code = @Code AND (@VehicleTypeId IS NULL OR VehicleTypeId <> @VehicleTypeId))
        THROW 50409, N'VEHICLE_TYPE_CODE_EXISTS|A vehicle type with this code already exists.', 1;

    IF @VehicleTypeId IS NULL
    BEGIN
        INSERT INTO mst.VehicleType (Code, Name, Description, CapacityKg, LengthFt, SortOrder, IsActive, CreatedBy)
        VALUES (UPPER(@Code), @Name, @Description, @CapacityKg, @LengthFt, @SortOrder, @IsActive, @UserId);
        SET @VehicleTypeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE mst.VehicleType
        SET Code = UPPER(@Code), Name = @Name, Description = @Description, CapacityKg = @CapacityKg, LengthFt = @LengthFt,
            SortOrder = @SortOrder, IsActive = @IsActive, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE VehicleTypeId = @VehicleTypeId;
        IF @@ROWCOUNT = 0 THROW 50404, N'VEHICLE_TYPE_NOT_FOUND|Vehicle type was not found.', 1;
    END

    SELECT CAST(@VehicleTypeId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_GoodsType_GetAll
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT gt.GoodsTypeId, gt.Code, gt.Name, gt.RequiresSpecialHandling, gt.IsActive
    FROM mst.GoodsType AS gt
    WHERE @IncludeInactive = 1 OR gt.IsActive = 1
    ORDER BY gt.Name;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_GoodsType_Save
    @GoodsTypeId              INT = NULL,
    @Code                     VARCHAR(40),
    @Name                     NVARCHAR(100),
    @RequiresSpecialHandling  BIT,
    @IsActive                 BIT,
    @UserId                   BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM mst.GoodsType WHERE Code = @Code AND (@GoodsTypeId IS NULL OR GoodsTypeId <> @GoodsTypeId))
        THROW 50409, N'GOODS_TYPE_CODE_EXISTS|A goods type with this code already exists.', 1;

    IF @GoodsTypeId IS NULL
    BEGIN
        INSERT INTO mst.GoodsType (Code, Name, RequiresSpecialHandling, IsActive, CreatedBy)
        VALUES (UPPER(@Code), @Name, @RequiresSpecialHandling, @IsActive, @UserId);
        SET @GoodsTypeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE mst.GoodsType
        SET Code = UPPER(@Code), Name = @Name, RequiresSpecialHandling = @RequiresSpecialHandling, IsActive = @IsActive,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE GoodsTypeId = @GoodsTypeId;
        IF @@ROWCOUNT = 0 THROW 50404, N'GOODS_TYPE_NOT_FOUND|Goods type was not found.', 1;
    END

    SELECT CAST(@GoodsTypeId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_DocumentType_GetAll
    @AppliesTo        VARCHAR(20) = NULL,
    @IncludeInactive  BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT dt.DocumentTypeId, dt.Code, dt.Name, dt.AppliesTo, dt.RequiresExpiry, dt.IsMandatory, dt.IsActive
    FROM mst.DocumentType AS dt
    WHERE (@AppliesTo IS NULL OR dt.AppliesTo = @AppliesTo)
      AND (@IncludeInactive = 1 OR dt.IsActive = 1)
    ORDER BY dt.AppliesTo, dt.Name;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_DocumentType_Save
    @DocumentTypeId  INT = NULL,
    @Code            VARCHAR(40),
    @Name            NVARCHAR(100),
    @AppliesTo       VARCHAR(20),
    @RequiresExpiry  BIT,
    @IsMandatory     BIT,
    @IsActive        BIT,
    @UserId          BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM mst.DocumentType WHERE Code = @Code AND (@DocumentTypeId IS NULL OR DocumentTypeId <> @DocumentTypeId))
        THROW 50409, N'DOCUMENT_TYPE_CODE_EXISTS|A document type with this code already exists.', 1;

    IF @DocumentTypeId IS NULL
    BEGIN
        INSERT INTO mst.DocumentType (Code, Name, AppliesTo, RequiresExpiry, IsMandatory, IsActive, CreatedBy)
        VALUES (UPPER(@Code), @Name, @AppliesTo, @RequiresExpiry, @IsMandatory, @IsActive, @UserId);
        SET @DocumentTypeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE mst.DocumentType
        SET Code = UPPER(@Code), Name = @Name, AppliesTo = @AppliesTo, RequiresExpiry = @RequiresExpiry,
            IsMandatory = @IsMandatory, IsActive = @IsActive, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DocumentTypeId = @DocumentTypeId;
        IF @@ROWCOUNT = 0 THROW 50404, N'DOCUMENT_TYPE_NOT_FOUND|Document type was not found.', 1;
    END

    SELECT CAST(@DocumentTypeId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_SystemSetting_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ss.SettingKey, ss.SettingValue, ss.DataType, ss.Description, ss.IsEditable, ss.ModifiedDateUtc
    FROM mst.SystemSetting AS ss
    ORDER BY ss.SettingKey;
END
GO

CREATE OR ALTER PROCEDURE mst.usp_SystemSetting_Update
    @SettingKey    VARCHAR(100),
    @SettingValue  NVARCHAR(1000),
    @ModifiedBy    BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DataType VARCHAR(20), @IsEditable BIT;
    SELECT @DataType = DataType, @IsEditable = IsEditable FROM mst.SystemSetting WHERE SettingKey = @SettingKey;

    IF @DataType IS NULL
        THROW 50404, N'SETTING_NOT_FOUND|Setting was not found.', 1;
    IF @IsEditable = 0
        THROW 50400, N'SETTING_READ_ONLY|This setting cannot be changed.', 1;
    IF (@DataType = 'Int' AND TRY_CAST(@SettingValue AS INT) IS NULL)
       OR (@DataType = 'Decimal' AND TRY_CAST(@SettingValue AS DECIMAL(18,4)) IS NULL)
       OR (@DataType = 'Bool' AND @SettingValue NOT IN (N'true', N'false'))
        THROW 50400, N'SETTING_VALUE_INVALID|The value does not match the setting data type.', 1;

    UPDATE mst.SystemSetting
    SET SettingValue = @SettingValue, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SettingKey = @SettingKey;
END
GO
