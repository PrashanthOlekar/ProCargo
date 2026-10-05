/*
    Scalar/inline functions shared by stored procedures.
    Scalar functions are kept deterministic and tiny (SQL Server 2019+ inlines them).
*/

-- Formats a business number: PC-<PREFIX>-<YYYY>-<000000>, e.g. PC-BKG-2026-000125.
CREATE OR ALTER FUNCTION core.fn_FormatBusinessNumber
(
    @Prefix      VARCHAR(5),
    @SequenceNo  BIGINT,
    @AtUtc       DATETIME2(3)
)
RETURNS VARCHAR(30)
WITH SCHEMABINDING
AS
BEGIN
    DECLARE @Seq VARCHAR(20) = CAST(@SequenceNo AS VARCHAR(20));
    RETURN 'PC-' + @Prefix + '-' + CAST(YEAR(@AtUtc) AS CHAR(4)) + '-'
         + CASE WHEN LEN(@Seq) >= 6 THEN @Seq ELSE RIGHT('000000' + @Seq, 6) END;
END
GO

-- Days until a compliance date (NULL date => NULL). Used by vehicle compliance views.
CREATE OR ALTER FUNCTION core.fn_DaysUntil
(
    @Date   DATE,
    @AtUtc  DATETIME2(3)
)
RETURNS INT
WITH SCHEMABINDING
AS
BEGIN
    RETURN CASE WHEN @Date IS NULL THEN NULL ELSE DATEDIFF(DAY, CAST(@AtUtc AS DATE), @Date) END;
END
GO

-- Applicable pricing rules for a quotation context (inline TVF => fully optimisable).
CREATE OR ALTER FUNCTION fin.fn_ApplicablePricingRules
(
    @VehicleTypeId   INT,
    @PickupCityId    INT,
    @DeliveryCityId  INT,
    @PickupStateId   INT,
    @AtUtc           DATETIME2(3)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        pr.PricingRuleId,
        pr.Name,
        pr.AdjustmentType,
        pr.AdjustmentValue,
        pr.Priority
    FROM fin.PricingRule AS pr
    WHERE pr.IsActive = 1
      AND pr.EffectiveFromUtc <= @AtUtc
      AND (pr.EffectiveToUtc IS NULL OR pr.EffectiveToUtc > @AtUtc)
      AND (pr.VehicleTypeId  IS NULL OR pr.VehicleTypeId  = @VehicleTypeId)
      AND (pr.StateId        IS NULL OR pr.StateId        = @PickupStateId)
      AND (pr.CityId         IS NULL OR pr.CityId         = @PickupCityId)
      AND (pr.PickupCityId   IS NULL OR pr.PickupCityId   = @PickupCityId)
      AND (pr.DeliveryCityId IS NULL OR pr.DeliveryCityId = @DeliveryCityId)
);
GO
