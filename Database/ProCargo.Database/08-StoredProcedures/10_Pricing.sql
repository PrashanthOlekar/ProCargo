/*
    Module: Pricing configuration (schema fin)
    Read procedures feed the API PricingService; Save procedures are used by Finance in the Operations portal.
    "Delete" is modelled as IsActive = 0 so historical quotations remain explainable.
*/

CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetVehiclePricing
    @VehicleTypeId INT,
    @AtUtc         DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) vp.VehiclePricingId, vp.VehicleTypeId, vp.BaseFare, vp.MinimumFare, vp.PerKmRate, vp.PerKgRate, vp.FreeWaitingHours
    FROM fin.VehiclePricing AS vp
    WHERE vp.VehicleTypeId = @VehicleTypeId
      AND vp.IsActive = 1
      AND vp.EffectiveFromUtc <= @AtUtc
      AND (vp.EffectiveToUtc IS NULL OR vp.EffectiveToUtc > @AtUtc)
    ORDER BY vp.EffectiveFromUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetDistanceSlabs
    @VehicleTypeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT dp.DistancePricingId, dp.VehicleTypeId, dp.FromKm, dp.ToKm, dp.RatePerKm
    FROM fin.DistancePricing AS dp
    WHERE dp.VehicleTypeId = @VehicleTypeId AND dp.IsActive = 1
    ORDER BY dp.FromKm;
END
GO

-- Vehicle-type specific charges override the generic charge with the same code.
CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetAdditionalCharges
    @VehicleTypeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT x.AdditionalChargeId, x.ChargeCode, x.Name, x.VehicleTypeId, x.CalculationType, x.Amount
    FROM
    (
        SELECT ac.AdditionalChargeId, ac.ChargeCode, ac.Name, ac.VehicleTypeId, ac.CalculationType, ac.Amount,
               ROW_NUMBER() OVER (PARTITION BY ac.ChargeCode ORDER BY CASE WHEN ac.VehicleTypeId IS NULL THEN 1 ELSE 0 END) AS rn
        FROM fin.AdditionalCharge AS ac
        WHERE ac.IsActive = 1 AND (ac.VehicleTypeId IS NULL OR ac.VehicleTypeId = @VehicleTypeId)
    ) AS x
    WHERE x.rn = 1
    ORDER BY x.ChargeCode;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetApplicableRules
    @VehicleTypeId   INT,
    @PickupCityId    INT,
    @DeliveryCityId  INT,
    @AtUtc           DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @PickupStateId INT = (SELECT StateId FROM mst.City WHERE CityId = @PickupCityId);

    SELECT r.PricingRuleId, r.Name, r.AdjustmentType, r.AdjustmentValue, r.Priority
    FROM fin.fn_ApplicablePricingRules(@VehicleTypeId, @PickupCityId, @DeliveryCityId, @PickupStateId, @AtUtc) AS r
    ORDER BY r.Priority, r.PricingRuleId;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetTaxRate
    @Code   VARCHAR(30),
    @AtUtc  DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) tr.TaxRateId, tr.Code, tr.Name, tr.RatePercent
    FROM fin.TaxRate AS tr
    WHERE tr.Code = @Code AND tr.IsActive = 1 AND tr.EffectiveFromUtc <= @AtUtc
      AND (tr.EffectiveToUtc IS NULL OR tr.EffectiveToUtc > @AtUtc)
    ORDER BY tr.EffectiveFromUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_Pricing_GetCommissionRule
    @VehicleTypeId INT,
    @AtUtc         DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) cr.CommissionRuleId, cr.Name, cr.CommissionPercent, cr.MinimumCommission
    FROM fin.CommissionRule AS cr
    WHERE cr.IsActive = 1 AND cr.EffectiveFromUtc <= @AtUtc
      AND (cr.EffectiveToUtc IS NULL OR cr.EffectiveToUtc > @AtUtc)
      AND (cr.VehicleTypeId IS NULL OR cr.VehicleTypeId = @VehicleTypeId)
    ORDER BY CASE WHEN cr.VehicleTypeId IS NULL THEN 1 ELSE 0 END, cr.EffectiveFromUtc DESC;
END
GO

-- ===== configuration lists =====

CREATE OR ALTER PROCEDURE fin.usp_VehiclePricing_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT vp.VehiclePricingId, vp.VehicleTypeId, vt.Name AS VehicleTypeName, vp.BaseFare, vp.MinimumFare, vp.PerKmRate, vp.PerKgRate,
           vp.FreeWaitingHours, vp.EffectiveFromUtc, vp.EffectiveToUtc, vp.IsActive
    FROM fin.VehiclePricing AS vp
    INNER JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = vp.VehicleTypeId
    ORDER BY vt.SortOrder, vp.EffectiveFromUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_VehiclePricing_Save
    @VehiclePricingId  INT = NULL,
    @VehicleTypeId     INT,
    @BaseFare          DECIMAL(12,2),
    @MinimumFare       DECIMAL(12,2),
    @PerKmRate         DECIMAL(10,2),
    @PerKgRate         DECIMAL(10,4),
    @FreeWaitingHours  DECIMAL(5,2),
    @EffectiveFromUtc  DATETIME2(3),
    @EffectiveToUtc    DATETIME2(3),
    @IsActive          BIT,
    @UserId            BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @VehiclePricingId IS NULL
    BEGIN
        INSERT INTO fin.VehiclePricing (VehicleTypeId, BaseFare, MinimumFare, PerKmRate, PerKgRate, FreeWaitingHours, EffectiveFromUtc, EffectiveToUtc, IsActive, CreatedBy)
        VALUES (@VehicleTypeId, @BaseFare, @MinimumFare, @PerKmRate, @PerKgRate, @FreeWaitingHours, @EffectiveFromUtc, @EffectiveToUtc, @IsActive, @UserId);
        SET @VehiclePricingId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.VehiclePricing
        SET VehicleTypeId = @VehicleTypeId, BaseFare = @BaseFare, MinimumFare = @MinimumFare, PerKmRate = @PerKmRate, PerKgRate = @PerKgRate,
            FreeWaitingHours = @FreeWaitingHours, EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc, IsActive = @IsActive,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE VehiclePricingId = @VehiclePricingId;
        IF @@ROWCOUNT = 0 THROW 50404, N'PRICING_NOT_FOUND|Vehicle pricing was not found.', 1;
    END

    SELECT CAST(@VehiclePricingId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_DistancePricing_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT dp.DistancePricingId, dp.VehicleTypeId, vt.Name AS VehicleTypeName, dp.FromKm, dp.ToKm, dp.RatePerKm, dp.IsActive
    FROM fin.DistancePricing AS dp
    INNER JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = dp.VehicleTypeId
    ORDER BY vt.SortOrder, dp.FromKm;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_DistancePricing_Save
    @DistancePricingId  INT = NULL,
    @VehicleTypeId      INT,
    @FromKm             DECIMAL(8,2),
    @ToKm               DECIMAL(8,2),
    @RatePerKm          DECIMAL(10,2),
    @IsActive           BIT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- active slabs of one vehicle type must not overlap
    IF @IsActive = 1 AND EXISTS
    (
        SELECT 1 FROM fin.DistancePricing AS dp
        WHERE dp.VehicleTypeId = @VehicleTypeId AND dp.IsActive = 1
          AND (@DistancePricingId IS NULL OR dp.DistancePricingId <> @DistancePricingId)
          AND dp.FromKm < ISNULL(@ToKm, 999999)
          AND ISNULL(dp.ToKm, 999999) > @FromKm
    )
        THROW 50409, N'DISTANCE_SLAB_OVERLAP|The distance range overlaps an existing active slab.', 1;

    IF @DistancePricingId IS NULL
    BEGIN
        INSERT INTO fin.DistancePricing (VehicleTypeId, FromKm, ToKm, RatePerKm, IsActive, CreatedBy)
        VALUES (@VehicleTypeId, @FromKm, @ToKm, @RatePerKm, @IsActive, @UserId);
        SET @DistancePricingId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.DistancePricing
        SET VehicleTypeId = @VehicleTypeId, FromKm = @FromKm, ToKm = @ToKm, RatePerKm = @RatePerKm, IsActive = @IsActive,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DistancePricingId = @DistancePricingId;
        IF @@ROWCOUNT = 0 THROW 50404, N'DISTANCE_SLAB_NOT_FOUND|Distance slab was not found.', 1;
    END

    SELECT CAST(@DistancePricingId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_AdditionalCharge_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ac.AdditionalChargeId, ac.ChargeCode, ac.Name, ac.VehicleTypeId, vt.Name AS VehicleTypeName, ac.CalculationType, ac.Amount, ac.IsActive
    FROM fin.AdditionalCharge AS ac
    LEFT JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = ac.VehicleTypeId
    ORDER BY ac.ChargeCode, vt.SortOrder;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_AdditionalCharge_Save
    @AdditionalChargeId  INT = NULL,
    @ChargeCode          VARCHAR(30),
    @Name                NVARCHAR(100),
    @VehicleTypeId       INT,
    @CalculationType     VARCHAR(20),
    @Amount              DECIMAL(12,2),
    @IsActive            BIT,
    @UserId              BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @IsActive = 1 AND EXISTS (SELECT 1 FROM fin.AdditionalCharge
                                 WHERE ChargeCode = @ChargeCode AND IsActive = 1
                                   AND ISNULL(VehicleTypeId, 0) = ISNULL(@VehicleTypeId, 0)
                                   AND (@AdditionalChargeId IS NULL OR AdditionalChargeId <> @AdditionalChargeId))
        THROW 50409, N'CHARGE_EXISTS|An active charge with this code already exists for the vehicle type.', 1;

    IF @AdditionalChargeId IS NULL
    BEGIN
        INSERT INTO fin.AdditionalCharge (ChargeCode, Name, VehicleTypeId, CalculationType, Amount, IsActive, CreatedBy)
        VALUES (UPPER(@ChargeCode), @Name, @VehicleTypeId, @CalculationType, @Amount, @IsActive, @UserId);
        SET @AdditionalChargeId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.AdditionalCharge
        SET ChargeCode = UPPER(@ChargeCode), Name = @Name, VehicleTypeId = @VehicleTypeId, CalculationType = @CalculationType,
            Amount = @Amount, IsActive = @IsActive, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE AdditionalChargeId = @AdditionalChargeId;
        IF @@ROWCOUNT = 0 THROW 50404, N'CHARGE_NOT_FOUND|Additional charge was not found.', 1;
    END

    SELECT CAST(@AdditionalChargeId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_PricingRule_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pr.PricingRuleId, pr.Name, pr.VehicleTypeId, vt.Name AS VehicleTypeName, pr.StateId, s.Name AS StateName,
           pr.CityId, c.Name AS CityName, pr.PickupCityId, pc.Name AS PickupCityName, pr.DeliveryCityId, dc.Name AS DeliveryCityName,
           pr.AdjustmentType, pr.AdjustmentValue, pr.Priority, pr.EffectiveFromUtc, pr.EffectiveToUtc, pr.IsActive
    FROM fin.PricingRule AS pr
    LEFT JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = pr.VehicleTypeId
    LEFT JOIN mst.State       AS s  ON s.StateId = pr.StateId
    LEFT JOIN mst.City        AS c  ON c.CityId = pr.CityId
    LEFT JOIN mst.City        AS pc ON pc.CityId = pr.PickupCityId
    LEFT JOIN mst.City        AS dc ON dc.CityId = pr.DeliveryCityId
    ORDER BY pr.IsActive DESC, pr.Priority, pr.Name;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_PricingRule_Save
    @PricingRuleId     INT = NULL,
    @Name              NVARCHAR(150),
    @VehicleTypeId     INT,
    @StateId           INT,
    @CityId            INT,
    @PickupCityId      INT,
    @DeliveryCityId    INT,
    @AdjustmentType    VARCHAR(20),
    @AdjustmentValue   DECIMAL(12,2),
    @Priority          INT,
    @EffectiveFromUtc  DATETIME2(3),
    @EffectiveToUtc    DATETIME2(3),
    @IsActive          BIT,
    @UserId            BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @PricingRuleId IS NULL
    BEGIN
        INSERT INTO fin.PricingRule (Name, VehicleTypeId, StateId, CityId, PickupCityId, DeliveryCityId, AdjustmentType, AdjustmentValue, Priority, EffectiveFromUtc, EffectiveToUtc, IsActive, CreatedBy)
        VALUES (@Name, @VehicleTypeId, @StateId, @CityId, @PickupCityId, @DeliveryCityId, @AdjustmentType, @AdjustmentValue, @Priority, @EffectiveFromUtc, @EffectiveToUtc, @IsActive, @UserId);
        SET @PricingRuleId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.PricingRule
        SET Name = @Name, VehicleTypeId = @VehicleTypeId, StateId = @StateId, CityId = @CityId, PickupCityId = @PickupCityId,
            DeliveryCityId = @DeliveryCityId, AdjustmentType = @AdjustmentType, AdjustmentValue = @AdjustmentValue, Priority = @Priority,
            EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc, IsActive = @IsActive,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE PricingRuleId = @PricingRuleId;
        IF @@ROWCOUNT = 0 THROW 50404, N'PRICING_RULE_NOT_FOUND|Pricing rule was not found.', 1;
    END

    SELECT CAST(@PricingRuleId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_TaxRate_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT tr.TaxRateId, tr.Code, tr.Name, tr.RatePercent, tr.EffectiveFromUtc, tr.EffectiveToUtc, tr.IsActive
    FROM fin.TaxRate AS tr
    ORDER BY tr.Code, tr.EffectiveFromUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_TaxRate_Save
    @TaxRateId         INT = NULL,
    @Code              VARCHAR(30),
    @Name              NVARCHAR(100),
    @RatePercent       DECIMAL(5,2),
    @EffectiveFromUtc  DATETIME2(3),
    @EffectiveToUtc    DATETIME2(3),
    @IsActive          BIT,
    @UserId            BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @TaxRateId IS NULL
    BEGIN
        INSERT INTO fin.TaxRate (Code, Name, RatePercent, EffectiveFromUtc, EffectiveToUtc, IsActive, CreatedBy)
        VALUES (UPPER(@Code), @Name, @RatePercent, @EffectiveFromUtc, @EffectiveToUtc, @IsActive, @UserId);
        SET @TaxRateId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.TaxRate
        SET Code = UPPER(@Code), Name = @Name, RatePercent = @RatePercent, EffectiveFromUtc = @EffectiveFromUtc,
            EffectiveToUtc = @EffectiveToUtc, IsActive = @IsActive
        WHERE TaxRateId = @TaxRateId;
        IF @@ROWCOUNT = 0 THROW 50404, N'TAX_RATE_NOT_FOUND|Tax rate was not found.', 1;
    END

    SELECT CAST(@TaxRateId AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_CommissionRule_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cr.CommissionRuleId, cr.Name, cr.VehicleTypeId, vt.Name AS VehicleTypeName, cr.CommissionPercent, cr.MinimumCommission,
           cr.EffectiveFromUtc, cr.EffectiveToUtc, cr.IsActive
    FROM fin.CommissionRule AS cr
    LEFT JOIN mst.VehicleType AS vt ON vt.VehicleTypeId = cr.VehicleTypeId
    ORDER BY cr.IsActive DESC, vt.SortOrder, cr.EffectiveFromUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE fin.usp_CommissionRule_Save
    @CommissionRuleId   INT = NULL,
    @Name               NVARCHAR(150),
    @VehicleTypeId      INT,
    @CommissionPercent  DECIMAL(5,2),
    @MinimumCommission  DECIMAL(12,2),
    @EffectiveFromUtc   DATETIME2(3),
    @EffectiveToUtc     DATETIME2(3),
    @IsActive           BIT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @CommissionRuleId IS NULL
    BEGIN
        INSERT INTO fin.CommissionRule (Name, VehicleTypeId, CommissionPercent, MinimumCommission, EffectiveFromUtc, EffectiveToUtc, IsActive, CreatedBy)
        VALUES (@Name, @VehicleTypeId, @CommissionPercent, @MinimumCommission, @EffectiveFromUtc, @EffectiveToUtc, @IsActive, @UserId);
        SET @CommissionRuleId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE fin.CommissionRule
        SET Name = @Name, VehicleTypeId = @VehicleTypeId, CommissionPercent = @CommissionPercent, MinimumCommission = @MinimumCommission,
            EffectiveFromUtc = @EffectiveFromUtc, EffectiveToUtc = @EffectiveToUtc, IsActive = @IsActive,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE CommissionRuleId = @CommissionRuleId;
        IF @@ROWCOUNT = 0 THROW 50404, N'COMMISSION_RULE_NOT_FOUND|Commission rule was not found.', 1;
    END

    SELECT CAST(@CommissionRuleId AS BIGINT) AS Id;
END
GO
