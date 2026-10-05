/*
    Module: Vehicle owners (schema core)
    Encrypted columns (PAN, bank account number) are written/read only as ciphertext; the API owns the keys.
*/

CREATE OR ALTER PROCEDURE core.usp_Owner_GetPaged
    @PageNumber            INT,
    @PageSize              INT,
    @Search                NVARCHAR(100) = NULL,
    @VerificationStatusId  INT           = NULL,
    @IsActive              BIT           = NULL,
    @SortBy                VARCHAR(50)   = 'CreatedDate',
    @SortDirection         VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OwnerId,
        o.OwnerNumber,
        o.OwnerTypeId,
        o.FullName,
        ob.BusinessName,
        o.Email,
        o.PhoneNumber,
        o.VerificationStatusId,
        o.IsActive,
        (SELECT COUNT(*) FROM core.Vehicle AS v WHERE v.OwnerId = o.OwnerId AND v.IsDeleted = 0) AS VehicleCount,
        (SELECT COUNT(*) FROM core.Driver  AS d WHERE d.OwnerId = o.OwnerId AND d.IsDeleted = 0) AS DriverCount,
        o.CreatedDateUtc,
        COUNT(*) OVER () AS TotalRecords
    FROM core.VehicleOwner AS o
    LEFT JOIN core.OwnerBusiness AS ob ON ob.OwnerId = o.OwnerId
    WHERE o.IsDeleted = 0
      AND (@VerificationStatusId IS NULL OR o.VerificationStatusId = @VerificationStatusId)
      AND (@IsActive IS NULL OR o.IsActive = @IsActive)
      AND (@Search IS NULL
           OR o.OwnerNumber LIKE @Search + '%'
           OR o.FullName LIKE N'%' + @Search + N'%'
           OR ob.BusinessName LIKE N'%' + @Search + N'%'
           OR o.Email LIKE @Search + N'%'
           OR o.PhoneNumber LIKE @Search + '%')
    ORDER BY
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'ASC'  THEN o.FullName END ASC,
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'DESC' THEN o.FullName END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN o.CreatedDateUtc END ASC,
        o.CreatedDateUtc DESC,
        o.OwnerId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_Owner_GetById
    @OwnerId BIGINT = NULL,
    @UserId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OwnerId,
        o.UserId,
        o.OwnerNumber,
        o.OwnerTypeId,
        o.FullName,
        o.Email,
        o.PhoneNumber,
        o.PanLast4,
        o.VerificationStatusId,
        o.VerificationRemarks,
        o.VerifiedDateUtc,
        o.IsActive,
        o.CreatedDateUtc,
        ob.BusinessName,
        ob.GstNumber,
        ob.RegistrationNumber,
        ob.FleetSize,
        o.RowVersion
    FROM core.VehicleOwner AS o
    LEFT JOIN core.OwnerBusiness AS ob ON ob.OwnerId = o.OwnerId
    WHERE o.IsDeleted = 0
      AND ((@OwnerId IS NOT NULL AND o.OwnerId = @OwnerId)
        OR (@OwnerId IS NULL AND o.UserId = @UserId));
END
GO

CREATE OR ALTER PROCEDURE core.usp_Owner_Update
    @OwnerId             BIGINT,
    @OwnerTypeId         INT,
    @FullName            NVARCHAR(150),
    @PhoneNumber         VARCHAR(20),
    @UpdatePan           BIT,
    @PanNumberEncrypted  NVARCHAR(500),
    @PanLast4            CHAR(4),
    @ModifiedBy          BIGINT,
    @RowVersion          BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.VehicleOwner WHERE OwnerId = @OwnerId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'OWNER_NOT_FOUND|Vehicle owner was not found.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND UserId <> @UserId AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|Another account already uses this phone number.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.VehicleOwner
        SET OwnerTypeId = @OwnerTypeId,
            FullName = @FullName,
            PhoneNumber = @PhoneNumber,
            PanNumberEncrypted = CASE WHEN @UpdatePan = 1 THEN @PanNumberEncrypted ELSE PanNumberEncrypted END,
            PanLast4 = CASE WHEN @UpdatePan = 1 THEN @PanLast4 ELSE PanLast4 END,
            -- KYC data changed after verification => needs review again
            VerificationStatusId = CASE WHEN @UpdatePan = 1 AND VerificationStatusId = 3 THEN 2 ELSE VerificationStatusId END,
            ModifiedBy = @ModifiedBy,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE OwnerId = @OwnerId AND RowVersion = @RowVersion;

        IF @@ROWCOUNT = 0
            THROW 50412, N'CONCURRENCY_CONFLICT|The owner was modified by someone else. Reload and try again.', 1;

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

CREATE OR ALTER PROCEDURE core.usp_OwnerBusiness_Save
    @OwnerId             BIGINT,
    @BusinessName        NVARCHAR(200),
    @GstNumber           VARCHAR(15),
    @RegistrationNumber  NVARCHAR(50),
    @FleetSize           INT,
    @UserId              BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @GstNumber IS NOT NULL AND EXISTS (SELECT 1 FROM core.OwnerBusiness WHERE GstNumber = @GstNumber AND OwnerId <> @OwnerId)
        THROW 50409, N'GST_ALREADY_REGISTERED|This GST number is registered to another owner.', 1;

    IF EXISTS (SELECT 1 FROM core.OwnerBusiness WHERE OwnerId = @OwnerId)
        UPDATE core.OwnerBusiness
        SET BusinessName = @BusinessName, GstNumber = @GstNumber, RegistrationNumber = @RegistrationNumber,
            FleetSize = @FleetSize, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE OwnerId = @OwnerId;
    ELSE
        INSERT INTO core.OwnerBusiness (OwnerId, BusinessName, GstNumber, RegistrationNumber, FleetSize, CreatedBy)
        VALUES (@OwnerId, @BusinessName, @GstNumber, @RegistrationNumber, @FleetSize, @UserId);
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerAddress_GetByOwner
    @OwnerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.OwnerAddressId,
        a.OwnerId,
        a.AddressLine1,
        a.AddressLine2,
        a.Landmark,
        a.CityId,
        c.Name AS CityName,
        c.StateId,
        s.Name AS StateName,
        a.Pincode,
        a.IsPrimary
    FROM core.OwnerAddress AS a
    INNER JOIN mst.City  AS c ON c.CityId = a.CityId
    INNER JOIN mst.State AS s ON s.StateId = c.StateId
    WHERE a.OwnerId = @OwnerId AND a.IsDeleted = 0
    ORDER BY a.IsPrimary DESC, a.OwnerAddressId;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerAddress_Save
    @OwnerAddressId  BIGINT        = NULL,
    @OwnerId         BIGINT,
    @AddressLine1    NVARCHAR(200),
    @AddressLine2    NVARCHAR(200),
    @Landmark        NVARCHAR(150),
    @CityId          INT,
    @Pincode         CHAR(6),
    @IsPrimary       BIT,
    @UserId          BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @IsPrimary = 1
            UPDATE core.OwnerAddress SET IsPrimary = 0 WHERE OwnerId = @OwnerId AND IsPrimary = 1;

        IF @OwnerAddressId IS NULL
        BEGIN
            INSERT INTO core.OwnerAddress (OwnerId, AddressLine1, AddressLine2, Landmark, CityId, Pincode, IsPrimary, CreatedBy)
            VALUES (@OwnerId, @AddressLine1, @AddressLine2, @Landmark, @CityId, @Pincode, @IsPrimary, @UserId);
            SET @OwnerAddressId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE core.OwnerAddress
            SET AddressLine1 = @AddressLine1, AddressLine2 = @AddressLine2, Landmark = @Landmark, CityId = @CityId,
                Pincode = @Pincode, IsPrimary = @IsPrimary, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE OwnerAddressId = @OwnerAddressId AND OwnerId = @OwnerId AND IsDeleted = 0;

            IF @@ROWCOUNT = 0
                THROW 50404, N'ADDRESS_NOT_FOUND|Address was not found.', 1;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @OwnerAddressId AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerAddress_Delete
    @OwnerAddressId  BIGINT,
    @OwnerId         BIGINT,
    @UserId          BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.OwnerAddress
    SET IsDeleted = 1, IsPrimary = 0, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OwnerAddressId = @OwnerAddressId AND OwnerId = @OwnerId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'ADDRESS_NOT_FOUND|Address was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerBankAccount_GetByOwner
    @OwnerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ba.OwnerBankAccountId,
        ba.OwnerId,
        ba.AccountHolderName,
        ba.BankName,
        ba.AccountNumberLast4,
        ba.IfscCode,
        ba.IsPrimary,
        ba.VerificationStatusId,
        ba.IsActive,
        ba.CreatedDateUtc
    FROM core.OwnerBankAccount AS ba
    WHERE ba.OwnerId = @OwnerId AND ba.IsActive = 1
    ORDER BY ba.IsPrimary DESC, ba.CreatedDateUtc DESC;
END
GO

-- Returns ciphertext for an explicit, audited "reveal" by finance users.
CREATE OR ALTER PROCEDURE core.usp_OwnerBankAccount_GetSensitive
    @OwnerBankAccountId BIGINT,
    @OwnerId            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ba.OwnerBankAccountId, ba.AccountHolderName, ba.BankName, ba.IfscCode, ba.AccountNumberEncrypted
    FROM core.OwnerBankAccount AS ba
    WHERE ba.OwnerBankAccountId = @OwnerBankAccountId AND ba.OwnerId = @OwnerId AND ba.IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerBankAccount_Create
    @OwnerId                 BIGINT,
    @AccountHolderName       NVARCHAR(150),
    @BankName                NVARCHAR(150),
    @AccountNumberEncrypted  NVARCHAR(500),
    @AccountNumberLast4      CHAR(4),
    @IfscCode                CHAR(11),
    @IsPrimary               BIT,
    @CreatedBy               BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Id BIGINT;

    BEGIN TRANSACTION;
        -- the first account is always primary
        IF NOT EXISTS (SELECT 1 FROM core.OwnerBankAccount WHERE OwnerId = @OwnerId AND IsActive = 1)
            SET @IsPrimary = 1;

        IF @IsPrimary = 1
            UPDATE core.OwnerBankAccount SET IsPrimary = 0 WHERE OwnerId = @OwnerId AND IsPrimary = 1;

        INSERT INTO core.OwnerBankAccount (OwnerId, AccountHolderName, BankName, AccountNumberEncrypted, AccountNumberLast4, IfscCode, IsPrimary, CreatedBy)
        VALUES (@OwnerId, @AccountHolderName, @BankName, @AccountNumberEncrypted, @AccountNumberLast4, UPPER(@IfscCode), @IsPrimary, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    COMMIT TRANSACTION;

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerBankAccount_Deactivate
    @OwnerBankAccountId BIGINT,
    @OwnerId            BIGINT,
    @ModifiedBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM fin.Settlement WHERE OwnerBankAccountId = @OwnerBankAccountId AND SettlementStatusId IN (1, 2, 3))
        THROW 50400, N'BANK_ACCOUNT_IN_USE|The account is used by a settlement that is in progress.', 1;

    UPDATE core.OwnerBankAccount
    SET IsActive = 0, IsPrimary = 0, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OwnerBankAccountId = @OwnerBankAccountId AND OwnerId = @OwnerId AND IsActive = 1;

    IF @@ROWCOUNT = 0
        THROW 50404, N'BANK_ACCOUNT_NOT_FOUND|Bank account was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_OwnerBankAccount_SetVerification
    @OwnerBankAccountId    BIGINT,
    @OwnerId               BIGINT,
    @VerificationStatusId  INT,
    @ModifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.OwnerBankAccount
    SET VerificationStatusId = @VerificationStatusId, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OwnerBankAccountId = @OwnerBankAccountId AND OwnerId = @OwnerId AND IsActive = 1;

    IF @@ROWCOUNT = 0
        THROW 50404, N'BANK_ACCOUNT_NOT_FOUND|Bank account was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Owner_SetVerification
    @OwnerId               BIGINT,
    @VerificationStatusId  INT,
    @Remarks               NVARCHAR(500),
    @VerifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.VehicleOwner
    SET VerificationStatusId = @VerificationStatusId,
        VerificationRemarks = @Remarks,
        VerifiedBy = @VerifiedBy,
        VerifiedDateUtc = SYSUTCDATETIME(),
        ModifiedBy = @VerifiedBy,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OwnerId = @OwnerId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'OWNER_NOT_FOUND|Vehicle owner was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_Owner_SetActive
    @OwnerId     BIGINT,
    @IsActive    BIT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.VehicleOwner WHERE OwnerId = @OwnerId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'OWNER_NOT_FOUND|Vehicle owner was not found.', 1;

    IF @IsActive = 0 AND EXISTS (SELECT 1 FROM core.Trip WHERE OwnerId = @OwnerId AND TripStatusId IN (1, 2, 3, 9, 10))
        THROW 50400, N'OWNER_HAS_ACTIVE_TRIPS|The owner has trips in progress.', 1;

    BEGIN TRANSACTION;
        UPDATE core.VehicleOwner SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE OwnerId = @OwnerId;

        UPDATE sec.[User] SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        IF @IsActive = 0
        BEGIN
            UPDATE core.Vehicle SET IsAvailable = 0 WHERE OwnerId = @OwnerId;
            UPDATE sec.RefreshToken SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'Deactivated'
            WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
        END
    COMMIT TRANSACTION;
END
GO
