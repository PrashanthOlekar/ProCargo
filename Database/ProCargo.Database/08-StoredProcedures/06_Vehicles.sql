/*
    Module: Vehicles (schema core)
    Vehicle numbers are stored normalised (upper case, no spaces/hyphens) by the API before calling.
*/

CREATE OR ALTER PROCEDURE core.usp_Vehicle_GetPaged
    @PageNumber            INT,
    @PageSize              INT,
    @Search                NVARCHAR(100) = NULL,
    @OwnerId               BIGINT        = NULL,
    @VehicleTypeId         INT           = NULL,
    @VerificationStatusId  INT           = NULL,
    @IsAvailable           BIT           = NULL,
    @IsActive              BIT           = NULL,
    @SortBy                VARCHAR(50)   = 'CreatedDate',
    @SortDirection         VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.VehicleId,
        v.VehicleNumber,
        v.OwnerId,
        o.FullName AS OwnerName,
        v.VehicleTypeId,
        vt.Name    AS VehicleTypeName,
        v.Manufacturer,
        v.Model,
        v.CapacityKg,
        v.VerificationStatusId,
        v.IsAvailable,
        v.IsActive,
        v.InsuranceExpiryDate,
        v.PermitExpiryDate,
        v.FitnessExpiryDate,
        v.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Vehicle AS v
    INNER JOIN core.VehicleOwner AS o  ON o.OwnerId = v.OwnerId
    INNER JOIN mst.VehicleType   AS vt ON vt.VehicleTypeId = v.VehicleTypeId
    WHERE v.IsDeleted = 0
      AND (@OwnerId IS NULL OR v.OwnerId = @OwnerId)
      AND (@VehicleTypeId IS NULL OR v.VehicleTypeId = @VehicleTypeId)
      AND (@VerificationStatusId IS NULL OR v.VerificationStatusId = @VerificationStatusId)
      AND (@IsAvailable IS NULL OR v.IsAvailable = @IsAvailable)
      AND (@IsActive IS NULL OR v.IsActive = @IsActive)
      AND (@Search IS NULL OR v.VehicleNumber LIKE N'%' + @Search + N'%' OR o.FullName LIKE N'%' + @Search + N'%' OR v.Model LIKE N'%' + @Search + N'%')
    ORDER BY
        CASE WHEN @SortBy = 'VehicleNumber'   AND @SortDirection = 'ASC'  THEN v.VehicleNumber END ASC,
        CASE WHEN @SortBy = 'VehicleNumber'   AND @SortDirection = 'DESC' THEN v.VehicleNumber END DESC,
        CASE WHEN @SortBy = 'InsuranceExpiry' AND @SortDirection = 'ASC'  THEN v.InsuranceExpiryDate END ASC,
        CASE WHEN @SortBy = 'InsuranceExpiry' AND @SortDirection = 'DESC' THEN v.InsuranceExpiryDate END DESC,
        CASE WHEN @SortBy = 'CreatedDate'     AND @SortDirection = 'ASC'  THEN v.CreatedDateUtc END ASC,
        v.CreatedDateUtc DESC,
        v.VehicleId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_GetById
    @VehicleId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.VehicleId,
        v.OwnerId,
        o.FullName AS OwnerName,
        v.VehicleNumber,
        v.VehicleTypeId,
        vt.Name    AS VehicleTypeName,
        v.Manufacturer,
        v.Model,
        v.ManufactureYear,
        v.CapacityKg,
        v.PermitNumber,
        v.PermitExpiryDate,
        v.InsuranceNumber,
        v.InsuranceExpiryDate,
        v.FitnessExpiryDate,
        v.PucExpiryDate,
        v.VerificationStatusId,
        v.VerificationRemarks,
        v.VerifiedDateUtc,
        v.IsAvailable,
        v.IsActive,
        v.CreatedDateUtc,
        v.RowVersion
    FROM core.Vehicle AS v
    INNER JOIN core.VehicleOwner AS o  ON o.OwnerId = v.OwnerId
    INNER JOIN mst.VehicleType   AS vt ON vt.VehicleTypeId = v.VehicleTypeId
    WHERE v.VehicleId = @VehicleId AND v.IsDeleted = 0;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_Create
    @OwnerId              BIGINT,
    @VehicleNumber        VARCHAR(15),
    @VehicleTypeId        INT,
    @Manufacturer         NVARCHAR(100),
    @Model                NVARCHAR(100),
    @ManufactureYear      INT,
    @CapacityKg           DECIMAL(10,2),
    @PermitNumber         VARCHAR(50),
    @PermitExpiryDate     DATE,
    @InsuranceNumber      VARCHAR(50),
    @InsuranceExpiryDate  DATE,
    @FitnessExpiryDate    DATE,
    @PucExpiryDate        DATE,
    @CreatedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM core.Vehicle WHERE VehicleNumber = @VehicleNumber)
        THROW 50409, N'VEHICLE_NUMBER_EXISTS|A vehicle with this registration number already exists.', 1;

    IF NOT EXISTS (SELECT 1 FROM core.VehicleOwner WHERE OwnerId = @OwnerId AND IsDeleted = 0 AND IsActive = 1)
        THROW 50400, N'OWNER_INACTIVE|Vehicles can only be added for an active owner.', 1;

    INSERT INTO core.Vehicle
        (OwnerId, VehicleNumber, VehicleTypeId, Manufacturer, Model, ManufactureYear, CapacityKg, PermitNumber, PermitExpiryDate,
         InsuranceNumber, InsuranceExpiryDate, FitnessExpiryDate, PucExpiryDate, CreatedBy)
    VALUES
        (@OwnerId, @VehicleNumber, @VehicleTypeId, @Manufacturer, @Model, @ManufactureYear, @CapacityKg, @PermitNumber, @PermitExpiryDate,
         @InsuranceNumber, @InsuranceExpiryDate, @FitnessExpiryDate, @PucExpiryDate, @CreatedBy);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_Update
    @VehicleId            BIGINT,
    @VehicleTypeId        INT,
    @Manufacturer         NVARCHAR(100),
    @Model                NVARCHAR(100),
    @ManufactureYear      INT,
    @CapacityKg           DECIMAL(10,2),
    @PermitNumber         VARCHAR(50),
    @PermitExpiryDate     DATE,
    @InsuranceNumber      VARCHAR(50),
    @InsuranceExpiryDate  DATE,
    @FitnessExpiryDate    DATE,
    @PucExpiryDate        DATE,
    @ModifiedBy           BIGINT,
    @RowVersion           BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE core.Vehicle
    SET VerificationStatusId =
            CASE WHEN VerificationStatusId = 3 AND (
                      VehicleTypeId <> @VehicleTypeId OR CapacityKg <> @CapacityKg
                   OR ISNULL(PermitNumber, '') <> ISNULL(@PermitNumber, '')
                   OR ISNULL(InsuranceNumber, '') <> ISNULL(@InsuranceNumber, ''))
                 THEN 2 ELSE VerificationStatusId END,
        VehicleTypeId = @VehicleTypeId,
        Manufacturer = @Manufacturer,
        Model = @Model,
        ManufactureYear = @ManufactureYear,
        CapacityKg = @CapacityKg,
        PermitNumber = @PermitNumber,
        PermitExpiryDate = @PermitExpiryDate,
        InsuranceNumber = @InsuranceNumber,
        InsuranceExpiryDate = @InsuranceExpiryDate,
        FitnessExpiryDate = @FitnessExpiryDate,
        PucExpiryDate = @PucExpiryDate,
        ModifiedBy = @ModifiedBy,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE VehicleId = @VehicleId AND IsDeleted = 0 AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM core.Vehicle WHERE VehicleId = @VehicleId AND IsDeleted = 0)
            THROW 50404, N'VEHICLE_NOT_FOUND|Vehicle was not found.', 1;
        THROW 50412, N'CONCURRENCY_CONFLICT|The vehicle was modified by someone else. Reload and try again.', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_SetAvailability
    @VehicleId    BIGINT,
    @IsAvailable  BIT,
    @Reason       NVARCHAR(300),
    @ChangedBy    BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM core.Trip WHERE VehicleId = @VehicleId AND TripStatusId IN (1, 2, 3, 9, 10))
        THROW 50400, N'VEHICLE_ON_TRIP|Availability cannot be changed while the vehicle is assigned to a trip.', 1;

    IF @IsAvailable = 1 AND NOT EXISTS (SELECT 1 FROM core.Vehicle WHERE VehicleId = @VehicleId AND VerificationStatusId = 3 AND IsActive = 1 AND IsDeleted = 0)
        THROW 50400, N'VEHICLE_NOT_VERIFIED|Only verified, active vehicles can be marked available.', 1;

    BEGIN TRANSACTION;
        UPDATE core.Vehicle
        SET IsAvailable = @IsAvailable, ModifiedBy = @ChangedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE VehicleId = @VehicleId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50404, N'VEHICLE_NOT_FOUND|Vehicle was not found.', 1;
        END

        INSERT INTO core.VehicleAvailabilityHistory (VehicleId, IsAvailable, Reason, ChangedBy)
        VALUES (@VehicleId, @IsAvailable, @Reason, @ChangedBy);
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_SetVerification
    @VehicleId             BIGINT,
    @VerificationStatusId  INT,
    @Remarks               NVARCHAR(500),
    @VerifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.Vehicle
    SET VerificationStatusId = @VerificationStatusId,
        VerificationRemarks = @Remarks,
        VerifiedBy = @VerifiedBy,
        VerifiedDateUtc = SYSUTCDATETIME(),
        IsAvailable = CASE WHEN @VerificationStatusId = 3 THEN IsAvailable ELSE 0 END,
        ModifiedBy = @VerifiedBy,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE VehicleId = @VehicleId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'VEHICLE_NOT_FOUND|Vehicle was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_SetActive
    @VehicleId   BIGINT,
    @IsActive    BIT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @IsActive = 0 AND EXISTS (SELECT 1 FROM core.Trip WHERE VehicleId = @VehicleId AND TripStatusId IN (1, 2, 3, 9, 10))
        THROW 50400, N'VEHICLE_ON_TRIP|The vehicle is assigned to a trip in progress.', 1;

    UPDATE core.Vehicle
    SET IsActive = @IsActive,
        IsAvailable = CASE WHEN @IsActive = 0 THEN 0 ELSE IsAvailable END,
        ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE VehicleId = @VehicleId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'VEHICLE_NOT_FOUND|Vehicle was not found.', 1;
END
GO

-- Candidates for trip assignment: verified, active, available, right type, enough capacity,
-- insurance valid on the planned pickup date.
CREATE OR ALTER PROCEDURE core.usp_Vehicle_GetAvailableForAssignment
    @VehicleTypeId  INT,
    @MinCapacityKg  DECIMAL(10,2),
    @OnDateUtc      DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        v.VehicleId,
        v.VehicleNumber,
        v.OwnerId,
        o.FullName AS OwnerName,
        v.VehicleTypeId,
        v.CapacityKg,
        v.InsuranceExpiryDate
    FROM core.Vehicle AS v
    INNER JOIN core.VehicleOwner AS o ON o.OwnerId = v.OwnerId AND o.IsActive = 1 AND o.VerificationStatusId = 3
    WHERE v.IsDeleted = 0
      AND v.IsActive = 1
      AND v.IsAvailable = 1
      AND v.VerificationStatusId = 3
      AND v.VehicleTypeId = @VehicleTypeId
      AND v.CapacityKg >= @MinCapacityKg
      AND (v.InsuranceExpiryDate IS NULL OR v.InsuranceExpiryDate >= CAST(@OnDateUtc AS DATE))
    ORDER BY v.CapacityKg, v.VehicleNumber;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Vehicle_GetExpiring
    @WithinDays  INT,
    @OwnerId     BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        vc.VehicleId,
        vc.VehicleNumber,
        vc.OwnerId,
        vc.OwnerName,
        vc.InsuranceExpiryDate,
        vc.PermitExpiryDate,
        vc.FitnessExpiryDate,
        vc.PucExpiryDate,
        vc.NextExpiryDate,
        vc.DaysToNextExpiry
    FROM rpt.vw_VehicleCompliance AS vc
    WHERE vc.NextExpiryDate IS NOT NULL
      AND vc.DaysToNextExpiry <= @WithinDays
      AND (@OwnerId IS NULL OR vc.OwnerId = @OwnerId)
    ORDER BY vc.NextExpiryDate;
END
GO
