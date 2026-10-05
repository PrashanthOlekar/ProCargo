/*
    Module: Identity & Security (schema sec)
    Error convention for every procedure in this project:
        THROW 504xx, N'ERROR_CODE|Human readable message', 1
        50400 business rule violation (HTTP 422)   50403 forbidden (403)
        50404 not found (404)                      50409 conflict (409)   50412 concurrency (409)
    The API translates these numbers into typed exceptions (Infrastructure/Persistence/SqlErrorTranslator).
*/

CREATE OR ALTER PROCEDURE sec.usp_User_Register
    @Email            NVARCHAR(256),
    @NormalizedEmail  NVARCHAR(256),
    @PhoneNumber      VARCHAR(20),
    @FullName         NVARCHAR(150),
    @PasswordHash     NVARCHAR(500),
    @AccountType      VARCHAR(20),      -- Customer | VehicleOwner | Driver
    @CustomerTypeId   INT           = NULL,
    @CompanyName      NVARCHAR(200) = NULL,
    @OwnerTypeId      INT           = NULL,
    @BusinessName     NVARCHAR(200) = NULL,
    @GstNumber        VARCHAR(15)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountType NOT IN ('Customer', 'VehicleOwner', 'Driver')
        THROW 50400, N'ACCOUNT_TYPE_INVALID|Account type is not supported for self-registration.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE NormalizedEmail = @NormalizedEmail AND IsDeleted = 0)
        THROW 50409, N'EMAIL_ALREADY_REGISTERED|An account with this e-mail already exists.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|An account with this phone number already exists.', 1;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @UserId BIGINT, @ProfileId BIGINT, @ProfileNumber VARCHAR(30);

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, CreatedDateUtc)
        VALUES (@Email, @NormalizedEmail, @PhoneNumber, @FullName, @PasswordHash, 0, @Now);
        SET @UserId = SCOPE_IDENTITY();

        UPDATE sec.[User] SET CreatedBy = @UserId WHERE UserId = @UserId;

        INSERT INTO sec.UserRole (UserId, RoleId, CreatedBy)
        SELECT @UserId, r.RoleId, @UserId FROM sec.Role AS r WHERE r.Name = @AccountType;

        IF @AccountType = 'Customer'
        BEGIN
            SET @ProfileNumber = core.fn_FormatBusinessNumber('CUS', NEXT VALUE FOR core.seq_CustomerNumber, @Now);
            INSERT INTO core.Customer (UserId, CustomerNumber, CustomerTypeId, FullName, CompanyName, Email, PhoneNumber, GstNumber, CreatedBy, CreatedDateUtc)
            VALUES (@UserId, @ProfileNumber, ISNULL(@CustomerTypeId, 1), @FullName, @CompanyName, @Email, @PhoneNumber, @GstNumber, @UserId, @Now);
            SET @ProfileId = SCOPE_IDENTITY();
        END
        ELSE IF @AccountType = 'VehicleOwner'
        BEGIN
            SET @ProfileNumber = core.fn_FormatBusinessNumber('OWN', NEXT VALUE FOR core.seq_OwnerNumber, @Now);
            INSERT INTO core.VehicleOwner (UserId, OwnerNumber, OwnerTypeId, FullName, Email, PhoneNumber, CreatedBy, CreatedDateUtc)
            VALUES (@UserId, @ProfileNumber, ISNULL(@OwnerTypeId, 1), @FullName, @Email, @PhoneNumber, @UserId, @Now);
            SET @ProfileId = SCOPE_IDENTITY();

            IF @BusinessName IS NOT NULL
                INSERT INTO core.OwnerBusiness (OwnerId, BusinessName, GstNumber, CreatedBy)
                VALUES (@ProfileId, @BusinessName, @GstNumber, @UserId);
        END
        ELSE
        BEGIN
            SET @ProfileNumber = core.fn_FormatBusinessNumber('DRV', NEXT VALUE FOR core.seq_DriverNumber, @Now);
            INSERT INTO core.Driver (UserId, DriverNumber, OwnerId, FullName, PhoneNumber, CreatedBy, CreatedDateUtc)
            VALUES (@UserId, @ProfileNumber, NULL, @FullName, @PhoneNumber, @UserId, @Now);
            SET @ProfileId = SCOPE_IDENTITY();
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @UserId AS UserId, @ProfileId AS ProfileId, @ProfileNumber AS ProfileNumber;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_CreateInternal
    @Email               NVARCHAR(256),
    @NormalizedEmail     NVARCHAR(256),
    @PhoneNumber         VARCHAR(20),
    @FullName            NVARCHAR(150),
    @PasswordHash        NVARCHAR(500),
    @RoleIdsJson         NVARCHAR(MAX),
    @MustChangePassword  BIT,
    @CreatedBy           BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Roles TABLE (RoleId INT PRIMARY KEY);
    INSERT INTO @Roles (RoleId) SELECT DISTINCT CAST([value] AS INT) FROM OPENJSON(@RoleIdsJson);

    IF NOT EXISTS (SELECT 1 FROM @Roles)
        THROW 50400, N'ROLE_REQUIRED|At least one role is required.', 1;

    IF EXISTS (SELECT 1 FROM @Roles AS x LEFT JOIN sec.Role AS r ON r.RoleId = x.RoleId WHERE r.RoleId IS NULL OR r.IsInternal = 0 OR r.IsActive = 0)
        THROW 50400, N'ROLE_INVALID|Internal users can only be given active internal roles.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE NormalizedEmail = @NormalizedEmail AND IsDeleted = 0)
        THROW 50409, N'EMAIL_ALREADY_REGISTERED|An account with this e-mail already exists.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|An account with this phone number already exists.', 1;

    DECLARE @UserId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, MustChangePassword, EmailConfirmed, CreatedBy)
        VALUES (@Email, @NormalizedEmail, @PhoneNumber, @FullName, @PasswordHash, 1, @MustChangePassword, 1, @CreatedBy);
        SET @UserId = SCOPE_IDENTITY();

        INSERT INTO sec.UserRole (UserId, RoleId, CreatedBy)
        SELECT @UserId, x.RoleId, @CreatedBy FROM @Roles AS x;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @UserId AS Id;
END
GO

-- Creates the first SuperAdmin when none exists (called once by the API bootstrapper).
CREATE OR ALTER PROCEDURE sec.usp_User_EnsureSuperAdmin
    @Email            NVARCHAR(256),
    @NormalizedEmail  NVARCHAR(256),
    @PhoneNumber      VARCHAR(20),
    @FullName         NVARCHAR(150),
    @PasswordHash     NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RoleId INT = (SELECT RoleId FROM sec.Role WHERE Name = 'SuperAdmin');

    IF EXISTS (SELECT 1 FROM sec.UserRole AS ur INNER JOIN sec.[User] AS u ON u.UserId = ur.UserId
               WHERE ur.RoleId = @RoleId AND u.IsDeleted = 0)
    BEGIN
        SELECT CAST(0 AS BIT) AS Value;
        RETURN;
    END

    BEGIN TRANSACTION;
        DECLARE @UserId BIGINT;
        INSERT INTO sec.[User] (Email, NormalizedEmail, PhoneNumber, FullName, PasswordHash, IsInternal, EmailConfirmed, MustChangePassword)
        VALUES (@Email, @NormalizedEmail, @PhoneNumber, @FullName, @PasswordHash, 1, 1, 1);
        SET @UserId = SCOPE_IDENTITY();
        INSERT INTO sec.UserRole (UserId, RoleId) VALUES (@UserId, @RoleId);
    COMMIT TRANSACTION;

    SELECT CAST(1 AS BIT) AS Value;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_GetAuthInfo
    @NormalizedEmail NVARCHAR(256) = NULL,
    @UserId          BIGINT        = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Email,
        u.FullName,
        u.PhoneNumber,
        u.PasswordHash,
        u.SecurityStamp,
        u.IsInternal,
        u.IsActive,
        u.FailedLoginCount,
        u.LockoutEndUtc,
        u.MustChangePassword,
        c.CustomerId,
        o.OwnerId,
        d.DriverId
    FROM sec.[User] AS u
    LEFT JOIN core.Customer     AS c ON c.UserId = u.UserId AND c.IsDeleted = 0
    LEFT JOIN core.VehicleOwner AS o ON o.UserId = u.UserId AND o.IsDeleted = 0
    LEFT JOIN core.Driver       AS d ON d.UserId = u.UserId AND d.IsDeleted = 0
    WHERE u.IsDeleted = 0
      AND ((@UserId IS NOT NULL AND u.UserId = @UserId)
        OR (@UserId IS NULL AND u.NormalizedEmail = @NormalizedEmail));
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_GetRolesAndPermissions
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 'Role' AS Kind, r.Name AS Code, r.IsInternal
    FROM sec.UserRole AS ur
    INNER JOIN sec.Role AS r ON r.RoleId = ur.RoleId
    WHERE ur.UserId = @UserId AND r.IsActive = 1
    UNION
    SELECT 'Permission' AS Kind, p.Code, p.IsInternal
    FROM sec.UserRole AS ur
    INNER JOIN sec.Role           AS r  ON r.RoleId = ur.RoleId AND r.IsActive = 1
    INNER JOIN sec.RolePermission AS rp ON rp.RoleId = r.RoleId
    INNER JOIN sec.Permission     AS p  ON p.PermissionId = rp.PermissionId
    WHERE ur.UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_RecordLoginSuccess
    @UserId          BIGINT,
    @AttemptedEmail  NVARCHAR(256),
    @Portal          VARCHAR(20),
    @IpAddress       VARCHAR(45),
    @UserAgent       NVARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;
        UPDATE sec.[User]
        SET FailedLoginCount = 0, LockoutEndUtc = NULL, LastLoginDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        INSERT INTO sec.LoginHistory (UserId, AttemptedEmail, IsSuccess, Portal, IpAddress, UserAgent)
        VALUES (@UserId, @AttemptedEmail, 1, @Portal, @IpAddress, LEFT(@UserAgent, 300));
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_RecordLoginFailure
    @UserId             BIGINT        = NULL,
    @AttemptedEmail     NVARCHAR(256),
    @Portal             VARCHAR(20),
    @FailureReason      VARCHAR(50),
    @IpAddress          VARCHAR(45),
    @UserAgent          NVARCHAR(300),
    @CountsTowardLockout BIT,
    @MaxFailedAttempts  INT,
    @LockoutMinutes     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Failed INT = 0, @LockoutEnd DATETIME2(3) = NULL;

    BEGIN TRANSACTION;
        IF @UserId IS NOT NULL AND @CountsTowardLockout = 1
        BEGIN
            UPDATE sec.[User]
            SET @Failed = FailedLoginCount = FailedLoginCount + 1,
                @LockoutEnd = LockoutEndUtc = CASE WHEN FailedLoginCount + 1 >= @MaxFailedAttempts
                                                   THEN DATEADD(MINUTE, @LockoutMinutes, SYSUTCDATETIME())
                                                   ELSE LockoutEndUtc END
            WHERE UserId = @UserId;

            -- reset the counter once the lockout is applied so the next window starts clean
            UPDATE sec.[User] SET FailedLoginCount = 0 WHERE UserId = @UserId AND FailedLoginCount >= @MaxFailedAttempts;
        END

        INSERT INTO sec.LoginHistory (UserId, AttemptedEmail, IsSuccess, FailureReason, Portal, IpAddress, UserAgent)
        VALUES (@UserId, @AttemptedEmail, 0, @FailureReason, @Portal, @IpAddress, LEFT(@UserAgent, 300));
    COMMIT TRANSACTION;

    SELECT @Failed AS FailedLoginCount, @LockoutEnd AS LockoutEndUtc;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_Create
    @UserId          BIGINT,
    @TokenHash       BINARY(32),
    @FamilyId        UNIQUEIDENTIFIER,
    @Portal          VARCHAR(20),
    @ExpiresDateUtc  DATETIME2(3),
    @CreatedByIp     VARCHAR(45),
    @UserAgent       NVARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO sec.RefreshToken (UserId, TokenHash, FamilyId, Portal, ExpiresDateUtc, CreatedByIp, UserAgent)
    VALUES (@UserId, @TokenHash, @FamilyId, @Portal, @ExpiresDateUtc, @CreatedByIp, LEFT(@UserAgent, 300));

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_GetByHash
    @TokenHash BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT rt.RefreshTokenId, rt.UserId, rt.FamilyId, rt.Portal, rt.ExpiresDateUtc, rt.RevokedDateUtc, rt.ReplacedByTokenId
    FROM sec.RefreshToken AS rt
    WHERE rt.TokenHash = @TokenHash;
END
GO

-- Atomically revokes the presented token and issues its successor in the same family.
-- If the token was already rotated by a concurrent request the UPDATE affects 0 rows -> conflict.
CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_Rotate
    @OldRefreshTokenId BIGINT,
    @NewTokenHash      BINARY(32),
    @ExpiresDateUtc    DATETIME2(3),
    @CreatedByIp       VARCHAR(45),
    @UserAgent         NVARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @NewId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO sec.RefreshToken (UserId, TokenHash, FamilyId, Portal, ExpiresDateUtc, CreatedByIp, UserAgent)
        SELECT rt.UserId, @NewTokenHash, rt.FamilyId, rt.Portal, @ExpiresDateUtc, @CreatedByIp, LEFT(@UserAgent, 300)
        FROM sec.RefreshToken AS rt WITH (UPDLOCK, HOLDLOCK)
        WHERE rt.RefreshTokenId = @OldRefreshTokenId AND rt.RevokedDateUtc IS NULL;

        IF @@ROWCOUNT = 0
            THROW 50409, N'REFRESH_TOKEN_REUSED|Refresh token has already been used.', 1;

        SET @NewId = SCOPE_IDENTITY();

        UPDATE sec.RefreshToken
        SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'Rotated', ReplacedByTokenId = @NewId
        WHERE RefreshTokenId = @OldRefreshTokenId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @NewId AS Id;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_RevokeFamily
    @FamilyId UNIQUEIDENTIFIER,
    @Reason   VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sec.RefreshToken
    SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = @Reason
    WHERE FamilyId = @FamilyId AND RevokedDateUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_Revoke
    @TokenHash BINARY(32),
    @Reason    VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sec.RefreshToken
    SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = @Reason
    WHERE TokenHash = @TokenHash AND RevokedDateUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_RefreshToken_RevokeAllForUser
    @UserId BIGINT,
    @Reason VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sec.RefreshToken
    SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = @Reason
    WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_PasswordResetToken_Create
    @UserId          BIGINT,
    @TokenHash       BINARY(32),
    @ExpiresDateUtc  DATETIME2(3),
    @RequestedIp     VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;
        -- only the newest reset link is valid
        UPDATE sec.PasswordResetToken
        SET ExpiresDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId AND UsedDateUtc IS NULL AND ExpiresDateUtc > SYSUTCDATETIME();

        INSERT INTO sec.PasswordResetToken (UserId, TokenHash, ExpiresDateUtc, RequestedIp)
        VALUES (@UserId, @TokenHash, @ExpiresDateUtc, @RequestedIp);
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_PasswordResetToken_Consume
    @TokenHash        BINARY(32),
    @NewPasswordHash  NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE prt
        SET @UserId = prt.UserId, UsedDateUtc = SYSUTCDATETIME()
        FROM sec.PasswordResetToken AS prt
        INNER JOIN sec.[User] AS u ON u.UserId = prt.UserId AND u.IsDeleted = 0 AND u.IsActive = 1
        WHERE prt.TokenHash = @TokenHash
          AND prt.UsedDateUtc IS NULL
          AND prt.ExpiresDateUtc > SYSUTCDATETIME();

        IF @UserId IS NULL
            THROW 50400, N'RESET_TOKEN_INVALID|The reset link is invalid or has expired.', 1;

        UPDATE sec.[User]
        SET PasswordHash = @NewPasswordHash, SecurityStamp = NEWID(), FailedLoginCount = 0,
            LockoutEndUtc = NULL, MustChangePassword = 0, EmailConfirmed = 1,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        UPDATE sec.RefreshToken
        SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'PasswordReset'
        WHERE UserId = @UserId AND RevokedDateUtc IS NULL;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @UserId AS Id;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_ChangePassword
    @UserId           BIGINT,
    @NewPasswordHash  NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;
        UPDATE sec.[User]
        SET PasswordHash = @NewPasswordHash, SecurityStamp = NEWID(), MustChangePassword = 0,
            ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId AND IsDeleted = 0;

        UPDATE sec.RefreshToken
        SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'PasswordChanged'
        WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_GetPaged
    @PageNumber     INT,
    @PageSize       INT,
    @Search         NVARCHAR(100) = NULL,
    @IsInternal     BIT           = NULL,
    @RoleId         INT           = NULL,
    @IsActive       BIT           = NULL,
    @SortBy         VARCHAR(50)   = 'CreatedDate',
    @SortDirection  VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Email,
        u.FullName,
        u.PhoneNumber,
        u.IsInternal,
        u.IsActive,
        CAST(CASE WHEN u.LockoutEndUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END AS BIT) AS IsLocked,
        u.LastLoginDateUtc,
        u.CreatedDateUtc,
        ISNULL(roles.RoleNames, N'') AS Roles,
        COUNT(*) OVER () AS TotalRecords
    FROM sec.[User] AS u
    OUTER APPLY
    (
        SELECT STRING_AGG(CAST(r.Name AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY r.Name) AS RoleNames
        FROM sec.UserRole AS ur INNER JOIN sec.Role AS r ON r.RoleId = ur.RoleId
        WHERE ur.UserId = u.UserId
    ) AS roles
    WHERE u.IsDeleted = 0
      AND (@IsInternal IS NULL OR u.IsInternal = @IsInternal)
      AND (@IsActive IS NULL OR u.IsActive = @IsActive)
      AND (@RoleId IS NULL OR EXISTS (SELECT 1 FROM sec.UserRole AS x WHERE x.UserId = u.UserId AND x.RoleId = @RoleId))
      AND (@Search IS NULL OR u.FullName LIKE N'%' + @Search + N'%' OR u.Email LIKE @Search + N'%' OR u.PhoneNumber LIKE @Search + '%')
    ORDER BY
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'ASC'  THEN u.FullName END ASC,
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'DESC' THEN u.FullName END DESC,
        CASE WHEN @SortBy = 'Email'       AND @SortDirection = 'ASC'  THEN u.Email END ASC,
        CASE WHEN @SortBy = 'Email'       AND @SortDirection = 'DESC' THEN u.Email END DESC,
        CASE WHEN @SortBy = 'LastLogin'   AND @SortDirection = 'ASC'  THEN u.LastLoginDateUtc END ASC,
        CASE WHEN @SortBy = 'LastLogin'   AND @SortDirection = 'DESC' THEN u.LastLoginDateUtc END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN u.CreatedDateUtc END ASC,
        u.CreatedDateUtc DESC,
        u.UserId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_GetById
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.UserId,
        u.Email,
        u.FullName,
        u.PhoneNumber,
        u.IsInternal,
        u.IsActive,
        u.EmailConfirmed,
        u.PhoneConfirmed,
        u.LockoutEndUtc,
        u.LastLoginDateUtc,
        u.MustChangePassword,
        u.CreatedDateUtc,
        ISNULL(roles.RoleNames, N'') AS Roles,
        u.RowVersion
    FROM sec.[User] AS u
    OUTER APPLY
    (
        SELECT STRING_AGG(CAST(r.Name AS NVARCHAR(MAX)), N', ') WITHIN GROUP (ORDER BY r.Name) AS RoleNames
        FROM sec.UserRole AS ur INNER JOIN sec.Role AS r ON r.RoleId = ur.RoleId
        WHERE ur.UserId = u.UserId
    ) AS roles
    WHERE u.UserId = @UserId AND u.IsDeleted = 0;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_GetRoleIds
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ur.RoleId AS Value FROM sec.UserRole AS ur WHERE ur.UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_Update
    @UserId       BIGINT,
    @FullName     NVARCHAR(150),
    @PhoneNumber  VARCHAR(20),
    @ModifiedBy   BIGINT,
    @RowVersion   BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND UserId <> @UserId AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|Another account already uses this phone number.', 1;

    UPDATE sec.[User]
    SET FullName = @FullName, PhoneNumber = @PhoneNumber, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE UserId = @UserId AND IsDeleted = 0 AND RowVersion = @RowVersion;

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sec.[User] WHERE UserId = @UserId AND IsDeleted = 0)
            THROW 50404, N'USER_NOT_FOUND|User was not found.', 1;
        THROW 50412, N'CONCURRENCY_CONFLICT|The user was modified by someone else. Reload and try again.', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_SetRoles
    @UserId       BIGINT,
    @RoleIdsJson  NVARCHAR(MAX),
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @IsInternal BIT = (SELECT IsInternal FROM sec.[User] WHERE UserId = @UserId AND IsDeleted = 0);
    IF @IsInternal IS NULL
        THROW 50404, N'USER_NOT_FOUND|User was not found.', 1;

    DECLARE @Roles TABLE (RoleId INT PRIMARY KEY);
    INSERT INTO @Roles (RoleId) SELECT DISTINCT CAST([value] AS INT) FROM OPENJSON(@RoleIdsJson);

    IF NOT EXISTS (SELECT 1 FROM @Roles)
        THROW 50400, N'ROLE_REQUIRED|At least one role is required.', 1;

    IF EXISTS (SELECT 1 FROM @Roles AS x LEFT JOIN sec.Role AS r ON r.RoleId = x.RoleId
               WHERE r.RoleId IS NULL OR r.IsInternal <> @IsInternal OR r.IsActive = 0)
        THROW 50400, N'ROLE_INVALID|Roles must exist, be active and match the user type (internal/external).', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DELETE FROM sec.UserRole WHERE UserId = @UserId AND RoleId NOT IN (SELECT RoleId FROM @Roles);

        INSERT INTO sec.UserRole (UserId, RoleId, CreatedBy)
        SELECT @UserId, x.RoleId, @ModifiedBy FROM @Roles AS x
        WHERE NOT EXISTS (SELECT 1 FROM sec.UserRole AS ur WHERE ur.UserId = @UserId AND ur.RoleId = x.RoleId);

        -- force re-authentication so new permissions are picked up
        UPDATE sec.RefreshToken SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'RolesChanged'
        WHERE UserId = @UserId AND RevokedDateUtc IS NULL;

        UPDATE sec.[User] SET SecurityStamp = NEWID(), ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_SetActive
    @UserId      BIGINT,
    @IsActive    BIT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;
        UPDATE sec.[User]
        SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId AND IsDeleted = 0;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50404, N'USER_NOT_FOUND|User was not found.', 1;
        END

        IF @IsActive = 0
            UPDATE sec.RefreshToken SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'Deactivated'
            WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_User_Unlock
    @UserId      BIGINT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sec.[User]
    SET LockoutEndUtc = NULL, FailedLoginCount = 0, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE UserId = @UserId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'USER_NOT_FOUND|User was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.RoleId,
        r.Name,
        r.Description,
        r.IsSystem,
        r.IsInternal,
        r.IsActive,
        (SELECT COUNT(*) FROM sec.UserRole AS ur WHERE ur.RoleId = r.RoleId)       AS UserCount,
        (SELECT COUNT(*) FROM sec.RolePermission AS rp WHERE rp.RoleId = r.RoleId) AS PermissionCount
    FROM sec.Role AS r
    ORDER BY r.IsInternal DESC, r.Name;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_GetPermissions
    @RoleId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT p.PermissionId, p.Code, p.Name, p.Module, p.IsInternal
    FROM sec.RolePermission AS rp
    INNER JOIN sec.Permission AS p ON p.PermissionId = rp.PermissionId
    WHERE rp.RoleId = @RoleId
    ORDER BY p.Module, p.Code;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Permission_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.PermissionId, p.Code, p.Name, p.Module, p.IsInternal
    FROM sec.Permission AS p
    ORDER BY p.IsInternal DESC, p.Module, p.Code;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_Create
    @Name         VARCHAR(50),
    @Description  NVARCHAR(200),
    @IsInternal   BIT,
    @CreatedBy    BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM sec.Role WHERE Name = @Name)
        THROW 50409, N'ROLE_NAME_EXISTS|A role with this name already exists.', 1;

    INSERT INTO sec.Role (Name, Description, IsSystem, IsInternal, CreatedBy)
    VALUES (@Name, @Description, 0, @IsInternal, @CreatedBy);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_Update
    @RoleId       INT,
    @Description  NVARCHAR(200),
    @IsActive     BIT,
    @ModifiedBy   BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM sec.Role WHERE RoleId = @RoleId AND IsSystem = 1 AND @IsActive = 0)
        THROW 50400, N'SYSTEM_ROLE_PROTECTED|System roles cannot be deactivated.', 1;

    UPDATE sec.Role
    SET Description = @Description, IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE RoleId = @RoleId;

    IF @@ROWCOUNT = 0
        THROW 50404, N'ROLE_NOT_FOUND|Role was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_SetPermissions
    @RoleId             INT,
    @PermissionIdsJson  NVARCHAR(MAX),
    @ModifiedBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RoleName VARCHAR(50), @IsInternal BIT;
    SELECT @RoleName = Name, @IsInternal = IsInternal FROM sec.Role WHERE RoleId = @RoleId;

    IF @RoleName IS NULL
        THROW 50404, N'ROLE_NOT_FOUND|Role was not found.', 1;

    IF @RoleName = 'SuperAdmin'
        THROW 50400, N'SYSTEM_ROLE_PROTECTED|SuperAdmin permissions cannot be changed.', 1;

    DECLARE @Perms TABLE (PermissionId INT PRIMARY KEY);
    INSERT INTO @Perms (PermissionId) SELECT DISTINCT CAST([value] AS INT) FROM OPENJSON(@PermissionIdsJson);

    IF EXISTS (SELECT 1 FROM @Perms AS x LEFT JOIN sec.Permission AS p ON p.PermissionId = x.PermissionId
               WHERE p.PermissionId IS NULL OR p.IsInternal <> @IsInternal)
        THROW 50400, N'PERMISSION_INVALID|Permissions must exist and match the role type (internal/external).', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        DELETE FROM sec.RolePermission WHERE RoleId = @RoleId AND PermissionId NOT IN (SELECT PermissionId FROM @Perms);

        INSERT INTO sec.RolePermission (RoleId, PermissionId, CreatedBy)
        SELECT @RoleId, x.PermissionId, @ModifiedBy FROM @Perms AS x
        WHERE NOT EXISTS (SELECT 1 FROM sec.RolePermission AS rp WHERE rp.RoleId = @RoleId AND rp.PermissionId = x.PermissionId);

        UPDATE rt SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'PermissionsChanged'
        FROM sec.RefreshToken AS rt
        INNER JOIN sec.UserRole AS ur ON ur.UserId = rt.UserId AND ur.RoleId = @RoleId
        WHERE rt.RevokedDateUtc IS NULL;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sec.usp_Role_Delete
    @RoleId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM sec.Role WHERE RoleId = @RoleId)
        THROW 50404, N'ROLE_NOT_FOUND|Role was not found.', 1;

    IF EXISTS (SELECT 1 FROM sec.Role WHERE RoleId = @RoleId AND IsSystem = 1)
        THROW 50400, N'SYSTEM_ROLE_PROTECTED|System roles cannot be deleted.', 1;

    IF EXISTS (SELECT 1 FROM sec.UserRole WHERE RoleId = @RoleId)
        THROW 50409, N'ROLE_IN_USE|The role is assigned to users.', 1;

    BEGIN TRANSACTION;
        DELETE FROM sec.RolePermission WHERE RoleId = @RoleId;
        DELETE FROM sec.Role WHERE RoleId = @RoleId;
    COMMIT TRANSACTION;
END
GO
