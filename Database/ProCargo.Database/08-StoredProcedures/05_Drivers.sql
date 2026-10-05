/*
    Module: Drivers (schema core)
    Availability ids: 1 Available, 2 OnTrip, 3 OffDuty, 4 Unavailable. OnTrip is only set by trip procedures.
*/

CREATE OR ALTER PROCEDURE core.usp_Driver_GetPaged
    @PageNumber            INT,
    @PageSize              INT,
    @Search                NVARCHAR(100) = NULL,
    @OwnerId               BIGINT        = NULL,
    @VerificationStatusId  INT           = NULL,
    @AvailabilityStatusId  INT           = NULL,
    @IsActive              BIT           = NULL,
    @SortBy                VARCHAR(50)   = 'CreatedDate',
    @SortDirection         VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.DriverId,
        d.DriverNumber,
        d.OwnerId,
        o.FullName AS OwnerName,
        d.FullName,
        d.PhoneNumber,
        dl.LicenseNumber,
        dl.ExpiryDate AS LicenseExpiryDate,
        d.VerificationStatusId,
        d.AvailabilityStatusId,
        d.IsActive,
        d.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Driver AS d
    LEFT JOIN core.VehicleOwner  AS o  ON o.OwnerId = d.OwnerId
    LEFT JOIN core.DriverLicense AS dl ON dl.DriverId = d.DriverId AND dl.IsCurrent = 1
    WHERE d.IsDeleted = 0
      AND (@OwnerId IS NULL OR d.OwnerId = @OwnerId)
      AND (@VerificationStatusId IS NULL OR d.VerificationStatusId = @VerificationStatusId)
      AND (@AvailabilityStatusId IS NULL OR d.AvailabilityStatusId = @AvailabilityStatusId)
      AND (@IsActive IS NULL OR d.IsActive = @IsActive)
      AND (@Search IS NULL
           OR d.DriverNumber LIKE @Search + '%'
           OR d.FullName LIKE N'%' + @Search + N'%'
           OR d.PhoneNumber LIKE @Search + '%'
           OR dl.LicenseNumber LIKE @Search + '%')
    ORDER BY
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'ASC'  THEN d.FullName END ASC,
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'DESC' THEN d.FullName END DESC,
        CASE WHEN @SortBy = 'LicenseExpiry' AND @SortDirection = 'ASC'  THEN dl.ExpiryDate END ASC,
        CASE WHEN @SortBy = 'LicenseExpiry' AND @SortDirection = 'DESC' THEN dl.ExpiryDate END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN d.CreatedDateUtc END ASC,
        d.CreatedDateUtc DESC,
        d.DriverId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_GetById
    @DriverId BIGINT = NULL,
    @UserId   BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.DriverId,
        d.UserId,
        d.DriverNumber,
        d.OwnerId,
        o.FullName AS OwnerName,
        d.FullName,
        u.Email,
        d.PhoneNumber,
        d.AlternatePhoneNumber,
        d.DateOfBirth,
        dl.LicenseNumber,
        dl.LicenseClass,
        dl.IssueDate          AS LicenseIssueDate,
        dl.ExpiryDate         AS LicenseExpiryDate,
        dl.IssuingAuthority   AS LicenseIssuingAuthority,
        d.VerificationStatusId,
        d.VerificationRemarks,
        d.VerifiedDateUtc,
        d.AvailabilityStatusId,
        d.IsActive,
        d.CreatedDateUtc,
        d.RowVersion
    FROM core.Driver AS d
    INNER JOIN sec.[User]        AS u  ON u.UserId = d.UserId
    LEFT  JOIN core.VehicleOwner AS o  ON o.OwnerId = d.OwnerId
    LEFT  JOIN core.DriverLicense AS dl ON dl.DriverId = d.DriverId AND dl.IsCurrent = 1
    WHERE d.IsDeleted = 0
      AND ((@DriverId IS NOT NULL AND d.DriverId = @DriverId)
        OR (@DriverId IS NULL AND d.UserId = @UserId));
END
GO

-- Owner / operations onboard a driver: creates the login (role Driver) and the driver profile.
CREATE OR ALTER PROCEDURE core.usp_Driver_CreateWithUser
    @OwnerId               BIGINT        = NULL,
    @FullName              NVARCHAR(150),
    @Email                 NVARCHAR(256),
    @NormalizedEmail       NVARCHAR(256),
    @PhoneNumber           VARCHAR(20),
    @AlternatePhoneNumber  VARCHAR(20),
    @DateOfBirth           DATE,
    @PasswordHash          NVARCHAR(500),
    @LicenseNumber         VARCHAR(20),
    @LicenseClass          VARCHAR(50),
    @LicenseIssueDate      DATE,
    @LicenseExpiryDate     DATE,
    @IssuingAuthority      NVARCHAR(100),
    @CreatedBy             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE NormalizedEmail = @NormalizedEmail AND IsDeleted = 0)
        THROW 50409, N'EMAIL_ALREADY_REGISTERED|An account with this e-mail already exists.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|An account with this phone number already exists.', 1;

    IF EXISTS (SELECT 1 FROM core.DriverLicense WHERE LicenseNumber = @LicenseNumber AND IsCurrent = 1)
        THROW 50409, N'LICENSE_ALREADY_REGISTERED|This licence number is already registered.', 1;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @UserId BIGINT, @DriverId BIGINT, @DriverNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, MustChangePassword, CreatedBy)
        VALUES (@Email, @NormalizedEmail, @PhoneNumber, @FullName, @PasswordHash, 0, 1, @CreatedBy);
        SET @UserId = SCOPE_IDENTITY();

        INSERT INTO sec.UserRole (UserId, RoleId, CreatedBy)
        SELECT @UserId, r.RoleId, @CreatedBy FROM sec.Role AS r WHERE r.Name = 'Driver';

        SET @DriverNumber = core.fn_FormatBusinessNumber('DRV', NEXT VALUE FOR core.seq_DriverNumber, @Now);

        INSERT INTO core.Driver (UserId, DriverNumber, OwnerId, FullName, PhoneNumber, AlternatePhoneNumber, DateOfBirth, CreatedBy, CreatedDateUtc)
        VALUES (@UserId, @DriverNumber, @OwnerId, @FullName, @PhoneNumber, @AlternatePhoneNumber, @DateOfBirth, @CreatedBy, @Now);
        SET @DriverId = SCOPE_IDENTITY();

        INSERT INTO core.DriverLicense (DriverId, LicenseNumber, LicenseClass, IssueDate, ExpiryDate, IssuingAuthority, IsCurrent, CreatedBy)
        VALUES (@DriverId, UPPER(@LicenseNumber), @LicenseClass, @LicenseIssueDate, @LicenseExpiryDate, @IssuingAuthority, 1, @CreatedBy);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @UserId AS UserId, @DriverId AS ProfileId, @DriverNumber AS ProfileNumber;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_Update
    @DriverId              BIGINT,
    @FullName              NVARCHAR(150),
    @PhoneNumber           VARCHAR(20),
    @AlternatePhoneNumber  VARCHAR(20),
    @DateOfBirth           DATE,
    @ModifiedBy            BIGINT,
    @RowVersion            BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.Driver WHERE DriverId = @DriverId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'DRIVER_NOT_FOUND|Driver was not found.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND UserId <> @UserId AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|Another account already uses this phone number.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Driver
        SET FullName = @FullName, PhoneNumber = @PhoneNumber, AlternatePhoneNumber = @AlternatePhoneNumber,
            DateOfBirth = @DateOfBirth, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DriverId = @DriverId AND RowVersion = @RowVersion;

        IF @@ROWCOUNT = 0
            THROW 50412, N'CONCURRENCY_CONFLICT|The driver was modified by someone else. Reload and try again.', 1;

        UPDATE sec.[User]
        SET FullName = @FullName, PhoneNumber = @PhoneNumber, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_SetLicense
    @DriverId          BIGINT,
    @LicenseNumber     VARCHAR(20),
    @LicenseClass      VARCHAR(50),
    @IssueDate         DATE,
    @ExpiryDate        DATE,
    @IssuingAuthority  NVARCHAR(100),
    @UserId            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @LicenseNumber = UPPER(@LicenseNumber);

    IF NOT EXISTS (SELECT 1 FROM core.Driver WHERE DriverId = @DriverId AND IsDeleted = 0)
        THROW 50404, N'DRIVER_NOT_FOUND|Driver was not found.', 1;

    IF EXISTS (SELECT 1 FROM core.DriverLicense WHERE LicenseNumber = @LicenseNumber AND IsCurrent = 1 AND DriverId <> @DriverId)
        THROW 50409, N'LICENSE_ALREADY_REGISTERED|This licence number is already registered to another driver.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.DriverLicense SET IsCurrent = 0, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DriverId = @DriverId AND IsCurrent = 1;

        INSERT INTO core.DriverLicense (DriverId, LicenseNumber, LicenseClass, IssueDate, ExpiryDate, IssuingAuthority, IsCurrent, CreatedBy)
        VALUES (@DriverId, @LicenseNumber, @LicenseClass, @IssueDate, @ExpiryDate, @IssuingAuthority, 1, @UserId);

        -- a changed licence must be re-verified
        UPDATE core.Driver
        SET VerificationStatusId = CASE WHEN VerificationStatusId = 3 THEN 2 ELSE VerificationStatusId END,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DriverId = @DriverId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_SetAvailability
    @DriverId              BIGINT,
    @AvailabilityStatusId  INT,
    @Reason                NVARCHAR(300),
    @ChangedBy             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AvailabilityStatusId = 2
        THROW 50400, N'AVAILABILITY_INVALID|On-trip status is set automatically by trip assignment.', 1;

    BEGIN TRANSACTION;
        UPDATE core.Driver
        SET AvailabilityStatusId = @AvailabilityStatusId, ModifiedBy = @ChangedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DriverId = @DriverId AND IsDeleted = 0 AND AvailabilityStatusId <> 2;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            IF EXISTS (SELECT 1 FROM core.Driver WHERE DriverId = @DriverId AND IsDeleted = 0)
                THROW 50400, N'DRIVER_ON_TRIP|Availability cannot be changed while the driver is on a trip.', 1;
            THROW 50404, N'DRIVER_NOT_FOUND|Driver was not found.', 1;
        END

        INSERT INTO core.DriverAvailabilityHistory (DriverId, AvailabilityStatusId, Reason, ChangedBy)
        VALUES (@DriverId, @AvailabilityStatusId, @Reason, @ChangedBy);
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_SetVerification
    @DriverId              BIGINT,
    @VerificationStatusId  INT,
    @Remarks               NVARCHAR(500),
    @VerifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.Driver
    SET VerificationStatusId = @VerificationStatusId,
        VerificationRemarks = @Remarks,
        VerifiedBy = @VerifiedBy,
        VerifiedDateUtc = SYSUTCDATETIME(),
        AvailabilityStatusId = CASE WHEN @VerificationStatusId <> 3 AND AvailabilityStatusId = 1 THEN 4 ELSE AvailabilityStatusId END,
        ModifiedBy = @VerifiedBy,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE DriverId = @DriverId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'DRIVER_NOT_FOUND|Driver was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Driver_SetActive
    @DriverId    BIGINT,
    @IsActive    BIT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.Driver WHERE DriverId = @DriverId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'DRIVER_NOT_FOUND|Driver was not found.', 1;

    IF @IsActive = 0 AND EXISTS (SELECT 1 FROM core.Trip WHERE DriverId = @DriverId AND TripStatusId IN (1, 2, 3, 9, 10))
        THROW 50400, N'DRIVER_HAS_ACTIVE_TRIP|The driver has a trip in progress.', 1;

    BEGIN TRANSACTION;
        UPDATE core.Driver
        SET IsActive = @IsActive,
            AvailabilityStatusId = CASE WHEN @IsActive = 0 THEN 4 ELSE AvailabilityStatusId END,
            ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE DriverId = @DriverId;

        UPDATE sec.[User] SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        IF @IsActive = 0
            UPDATE sec.RefreshToken SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'Deactivated'
            WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
    COMMIT TRANSACTION;
END
GO

-- Verified, active, available drivers whose licence is valid on the planned date.
CREATE OR ALTER PROCEDURE core.usp_Driver_GetAvailableForAssignment
    @OwnerId      BIGINT = NULL,
    @OnDateUtc    DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.DriverId,
        d.DriverNumber,
        d.FullName,
        d.PhoneNumber,
        d.OwnerId,
        dl.ExpiryDate AS LicenseExpiryDate
    FROM core.Driver AS d
    INNER JOIN core.DriverLicense AS dl ON dl.DriverId = d.DriverId AND dl.IsCurrent = 1
    WHERE d.IsDeleted = 0
      AND d.IsActive = 1
      AND d.VerificationStatusId = 3
      AND d.AvailabilityStatusId = 1
      AND dl.ExpiryDate >= CAST(@OnDateUtc AS DATE)
      AND (@OwnerId IS NULL OR d.OwnerId = @OwnerId OR d.OwnerId IS NULL)
    ORDER BY CASE WHEN d.OwnerId = @OwnerId THEN 0 ELSE 1 END, d.FullName;
END
GO
