/*
    Configurable pricing. Nothing about price is hard-coded in the API or the UIs.

    Calculation (implemented by ProCargo.Domain PricingCalculator):
      base       = VehiclePricing.BaseFare
      distance   = sum over DistancePricing slabs (or VehiclePricing.PerKmRate when no slab matches)
      weight     = TotalWeightKg * VehiclePricing.PerKgRate
      extras     = AdditionalCharge rows (loading, unloading, waiting/hour, night, special handling)
      rules      = PricingRule adjustments (state/city/route/vehicle type/season via effective dates)
      floor      = max(subtotal, VehiclePricing.MinimumFare)
      tax        = TaxRate.RatePercent
    Every table is effective-dated so prices can be changed without rewriting history.
*/

IF OBJECT_ID(N'fin.VehiclePricing', N'U') IS NULL
BEGIN
    CREATE TABLE fin.VehiclePricing
    (
        VehiclePricingId   INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_VehiclePricing PRIMARY KEY,
        VehicleTypeId      INT            NOT NULL,
        BaseFare           DECIMAL(12,2)  NOT NULL CONSTRAINT CK_VehiclePricing_BaseFare CHECK (BaseFare >= 0),
        MinimumFare        DECIMAL(12,2)  NOT NULL CONSTRAINT CK_VehiclePricing_MinimumFare CHECK (MinimumFare >= 0),
        PerKmRate          DECIMAL(10,2)  NOT NULL CONSTRAINT CK_VehiclePricing_PerKmRate CHECK (PerKmRate >= 0),
        PerKgRate          DECIMAL(10,4)  NOT NULL CONSTRAINT DF_VehiclePricing_PerKgRate DEFAULT (0),
        FreeWaitingHours   DECIMAL(5,2)   NOT NULL CONSTRAINT DF_VehiclePricing_FreeWaitingHours DEFAULT (2),
        EffectiveFromUtc   DATETIME2(3)   NOT NULL,
        EffectiveToUtc     DATETIME2(3)   NULL,
        IsActive           BIT            NOT NULL CONSTRAINT DF_VehiclePricing_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_VehiclePricing_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL,
        CONSTRAINT CK_VehiclePricing_Effective CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc)
    );
END
GO

IF OBJECT_ID(N'fin.DistancePricing', N'U') IS NULL
BEGIN
    CREATE TABLE fin.DistancePricing
    (
        DistancePricingId  INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_DistancePricing PRIMARY KEY,
        VehicleTypeId      INT            NOT NULL,
        FromKm             DECIMAL(8,2)   NOT NULL CONSTRAINT CK_DistancePricing_FromKm CHECK (FromKm >= 0),
        ToKm               DECIMAL(8,2)   NULL,
        RatePerKm          DECIMAL(10,2)  NOT NULL CONSTRAINT CK_DistancePricing_RatePerKm CHECK (RatePerKm >= 0),
        IsActive           BIT            NOT NULL CONSTRAINT DF_DistancePricing_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_DistancePricing_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL,
        CONSTRAINT CK_DistancePricing_Range CHECK (ToKm IS NULL OR ToKm > FromKm)
    );
END
GO

IF OBJECT_ID(N'fin.AdditionalCharge', N'U') IS NULL
BEGIN
    CREATE TABLE fin.AdditionalCharge
    (
        AdditionalChargeId INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdditionalCharge PRIMARY KEY,
        ChargeCode         VARCHAR(30)    NOT NULL,
        Name               NVARCHAR(100)  NOT NULL,
        VehicleTypeId      INT            NULL,
        CalculationType    VARCHAR(20)    NOT NULL CONSTRAINT CK_AdditionalCharge_CalculationType CHECK (CalculationType IN ('Flat','PerHour','PerKm','Percentage')),
        Amount             DECIMAL(12,2)  NOT NULL CONSTRAINT CK_AdditionalCharge_Amount CHECK (Amount >= 0),
        IsActive           BIT            NOT NULL CONSTRAINT DF_AdditionalCharge_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_AdditionalCharge_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL
    );
END
GO

IF OBJECT_ID(N'fin.PricingRule', N'U') IS NULL
BEGIN
    CREATE TABLE fin.PricingRule
    (
        PricingRuleId      INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_PricingRule PRIMARY KEY,
        Name               NVARCHAR(150)  NOT NULL,
        VehicleTypeId      INT            NULL,
        StateId            INT            NULL,
        CityId             INT            NULL,
        PickupCityId       INT            NULL,
        DeliveryCityId     INT            NULL,
        AdjustmentType     VARCHAR(20)    NOT NULL CONSTRAINT CK_PricingRule_AdjustmentType CHECK (AdjustmentType IN ('Percentage','Flat')),
        AdjustmentValue    DECIMAL(12,2)  NOT NULL,
        Priority           INT            NOT NULL CONSTRAINT DF_PricingRule_Priority DEFAULT (100),
        EffectiveFromUtc   DATETIME2(3)   NOT NULL,
        EffectiveToUtc     DATETIME2(3)   NULL,
        IsActive           BIT            NOT NULL CONSTRAINT DF_PricingRule_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_PricingRule_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL,
        CONSTRAINT CK_PricingRule_Effective CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc),
        CONSTRAINT CK_PricingRule_Percentage CHECK (AdjustmentType <> 'Percentage' OR AdjustmentValue BETWEEN -90 AND 500)
    );
END
GO

IF OBJECT_ID(N'fin.TaxRate', N'U') IS NULL
BEGIN
    CREATE TABLE fin.TaxRate
    (
        TaxRateId          INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaxRate PRIMARY KEY,
        Code               VARCHAR(30)    NOT NULL,
        Name               NVARCHAR(100)  NOT NULL,
        RatePercent        DECIMAL(5,2)   NOT NULL CONSTRAINT CK_TaxRate_RatePercent CHECK (RatePercent BETWEEN 0 AND 100),
        EffectiveFromUtc   DATETIME2(3)   NOT NULL,
        EffectiveToUtc     DATETIME2(3)   NULL,
        IsActive           BIT            NOT NULL CONSTRAINT DF_TaxRate_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_TaxRate_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'fin.CommissionRule', N'U') IS NULL
BEGIN
    CREATE TABLE fin.CommissionRule
    (
        CommissionRuleId   INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommissionRule PRIMARY KEY,
        Name               NVARCHAR(150)  NOT NULL,
        VehicleTypeId      INT            NULL,
        CommissionPercent  DECIMAL(5,2)   NOT NULL CONSTRAINT CK_CommissionRule_Percent CHECK (CommissionPercent BETWEEN 0 AND 100),
        MinimumCommission  DECIMAL(12,2)  NOT NULL CONSTRAINT DF_CommissionRule_MinimumCommission DEFAULT (0),
        EffectiveFromUtc   DATETIME2(3)   NOT NULL,
        EffectiveToUtc     DATETIME2(3)   NULL,
        IsActive           BIT            NOT NULL CONSTRAINT DF_CommissionRule_IsActive DEFAULT (1),
        CreatedBy          BIGINT         NULL,
        CreatedDateUtc     DATETIME2(3)   NOT NULL CONSTRAINT DF_CommissionRule_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT         NULL,
        ModifiedDateUtc    DATETIME2(3)   NULL
    );
END
GO
