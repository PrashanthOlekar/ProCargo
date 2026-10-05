/*
    Module: Customers (schema core)
    Child-row procedures always take the parent id as well (e.g. @CustomerId) and include it in
    the WHERE clause, so a forged child id can never touch another customer's data (defence in depth
    on top of the API's resource-level authorization).
*/

CREATE OR ALTER PROCEDURE core.usp_Customer_GetPaged
    @PageNumber      INT,
    @PageSize        INT,
    @Search          NVARCHAR(100) = NULL,
    @CustomerTypeId  INT           = NULL,
    @IsActive        BIT           = NULL,
    @SortBy          VARCHAR(50)   = 'CreatedDate',
    @SortDirection   VARCHAR(4)    = 'DESC'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.CustomerId,
        c.CustomerNumber,
        c.CustomerTypeId,
        c.FullName,
        c.CompanyName,
        c.Email,
        c.PhoneNumber,
        c.IsActive,
        c.CreatedDateUtc,
        (SELECT COUNT(*) FROM core.Booking AS b WHERE b.CustomerId = c.CustomerId) AS BookingCount,
        COUNT(*) OVER () AS TotalRecords
    FROM core.Customer AS c
    WHERE c.IsDeleted = 0
      AND (@CustomerTypeId IS NULL OR c.CustomerTypeId = @CustomerTypeId)
      AND (@IsActive IS NULL OR c.IsActive = @IsActive)
      AND (@Search IS NULL
           OR c.CustomerNumber LIKE @Search + '%'
           OR c.FullName LIKE N'%' + @Search + N'%'
           OR c.CompanyName LIKE N'%' + @Search + N'%'
           OR c.Email LIKE @Search + N'%'
           OR c.PhoneNumber LIKE @Search + '%')
    ORDER BY
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'ASC'  THEN c.FullName END ASC,
        CASE WHEN @SortBy = 'Name'        AND @SortDirection = 'DESC' THEN c.FullName END DESC,
        CASE WHEN @SortBy = 'Number'      AND @SortDirection = 'ASC'  THEN c.CustomerNumber END ASC,
        CASE WHEN @SortBy = 'Number'      AND @SortDirection = 'DESC' THEN c.CustomerNumber END DESC,
        CASE WHEN @SortBy = 'CreatedDate' AND @SortDirection = 'ASC'  THEN c.CreatedDateUtc END ASC,
        c.CreatedDateUtc DESC,
        c.CustomerId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_Customer_GetById
    @CustomerId BIGINT = NULL,
    @UserId     BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.CustomerId,
        c.UserId,
        c.CustomerNumber,
        c.CustomerTypeId,
        c.FullName,
        c.CompanyName,
        c.Email,
        c.PhoneNumber,
        c.GstNumber,
        c.IsActive,
        c.CreatedDateUtc,
        c.RowVersion
    FROM core.Customer AS c
    WHERE c.IsDeleted = 0
      AND ((@CustomerId IS NOT NULL AND c.CustomerId = @CustomerId)
        OR (@CustomerId IS NULL AND c.UserId = @UserId));
END
GO

CREATE OR ALTER PROCEDURE core.usp_Customer_Update
    @CustomerId      BIGINT,
    @CustomerTypeId  INT,
    @FullName        NVARCHAR(150),
    @CompanyName     NVARCHAR(200),
    @PhoneNumber     VARCHAR(20),
    @GstNumber       VARCHAR(15),
    @ModifiedBy      BIGINT,
    @RowVersion      BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.Customer WHERE CustomerId = @CustomerId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'CUSTOMER_NOT_FOUND|Customer was not found.', 1;

    IF EXISTS (SELECT 1 FROM sec.[User] WHERE PhoneNumber = @PhoneNumber AND UserId <> @UserId AND IsDeleted = 0)
        THROW 50409, N'PHONE_ALREADY_REGISTERED|Another account already uses this phone number.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE core.Customer
        SET CustomerTypeId = @CustomerTypeId, FullName = @FullName, CompanyName = @CompanyName, PhoneNumber = @PhoneNumber,
            GstNumber = @GstNumber, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE CustomerId = @CustomerId AND RowVersion = @RowVersion;

        IF @@ROWCOUNT = 0
            THROW 50412, N'CONCURRENCY_CONFLICT|The customer was modified by someone else. Reload and try again.', 1;

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

CREATE OR ALTER PROCEDURE core.usp_Customer_SetActive
    @CustomerId  BIGINT,
    @IsActive    BIT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId BIGINT = (SELECT UserId FROM core.Customer WHERE CustomerId = @CustomerId AND IsDeleted = 0);
    IF @UserId IS NULL
        THROW 50404, N'CUSTOMER_NOT_FOUND|Customer was not found.', 1;

    BEGIN TRANSACTION;
        UPDATE core.Customer SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE CustomerId = @CustomerId;

        UPDATE sec.[User] SET IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE UserId = @UserId;

        IF @IsActive = 0
            UPDATE sec.RefreshToken SET RevokedDateUtc = SYSUTCDATETIME(), RevokedReason = 'Deactivated'
            WHERE UserId = @UserId AND RevokedDateUtc IS NULL;
    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerAddress_GetByCustomer
    @CustomerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        a.CustomerAddressId,
        a.CustomerId,
        a.Label,
        a.AddressLine1,
        a.AddressLine2,
        a.Landmark,
        a.CityId,
        c.Name  AS CityName,
        c.StateId,
        s.Name  AS StateName,
        a.Pincode,
        a.Latitude,
        a.Longitude,
        a.IsDefault
    FROM core.CustomerAddress AS a
    INNER JOIN mst.City  AS c ON c.CityId = a.CityId
    INNER JOIN mst.State AS s ON s.StateId = c.StateId
    WHERE a.CustomerId = @CustomerId AND a.IsDeleted = 0
    ORDER BY a.IsDefault DESC, a.Label;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerAddress_Save
    @CustomerAddressId  BIGINT        = NULL,
    @CustomerId         BIGINT,
    @Label              NVARCHAR(50),
    @AddressLine1       NVARCHAR(200),
    @AddressLine2       NVARCHAR(200),
    @Landmark           NVARCHAR(150),
    @CityId             INT,
    @Pincode            CHAR(6),
    @Latitude           DECIMAL(9,6),
    @Longitude          DECIMAL(9,6),
    @IsDefault          BIT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @IsDefault = 1
            UPDATE core.CustomerAddress SET IsDefault = 0 WHERE CustomerId = @CustomerId AND IsDefault = 1;

        IF @CustomerAddressId IS NULL
        BEGIN
            INSERT INTO core.CustomerAddress (CustomerId, Label, AddressLine1, AddressLine2, Landmark, CityId, Pincode, Latitude, Longitude, IsDefault, CreatedBy)
            VALUES (@CustomerId, @Label, @AddressLine1, @AddressLine2, @Landmark, @CityId, @Pincode, @Latitude, @Longitude, @IsDefault, @UserId);
            SET @CustomerAddressId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE core.CustomerAddress
            SET Label = @Label, AddressLine1 = @AddressLine1, AddressLine2 = @AddressLine2, Landmark = @Landmark, CityId = @CityId,
                Pincode = @Pincode, Latitude = @Latitude, Longitude = @Longitude, IsDefault = @IsDefault,
                ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE CustomerAddressId = @CustomerAddressId AND CustomerId = @CustomerId AND IsDeleted = 0;

            IF @@ROWCOUNT = 0
                THROW 50404, N'ADDRESS_NOT_FOUND|Address was not found.', 1;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @CustomerAddressId AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerAddress_Delete
    @CustomerAddressId  BIGINT,
    @CustomerId         BIGINT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.CustomerAddress
    SET IsDeleted = 1, IsDefault = 0, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE CustomerAddressId = @CustomerAddressId AND CustomerId = @CustomerId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'ADDRESS_NOT_FOUND|Address was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerContact_GetByCustomer
    @CustomerId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cc.CustomerContactId, cc.CustomerId, cc.ContactName, cc.PhoneNumber, cc.Email, cc.Designation, cc.IsPrimary
    FROM core.CustomerContact AS cc
    WHERE cc.CustomerId = @CustomerId AND cc.IsDeleted = 0
    ORDER BY cc.IsPrimary DESC, cc.ContactName;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerContact_Save
    @CustomerContactId  BIGINT        = NULL,
    @CustomerId         BIGINT,
    @ContactName        NVARCHAR(150),
    @PhoneNumber        VARCHAR(20),
    @Email              NVARCHAR(256),
    @Designation        NVARCHAR(100),
    @IsPrimary          BIT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @IsPrimary = 1
            UPDATE core.CustomerContact SET IsPrimary = 0 WHERE CustomerId = @CustomerId AND IsPrimary = 1;

        IF @CustomerContactId IS NULL
        BEGIN
            INSERT INTO core.CustomerContact (CustomerId, ContactName, PhoneNumber, Email, Designation, IsPrimary, CreatedBy)
            VALUES (@CustomerId, @ContactName, @PhoneNumber, @Email, @Designation, @IsPrimary, @UserId);
            SET @CustomerContactId = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE core.CustomerContact
            SET ContactName = @ContactName, PhoneNumber = @PhoneNumber, Email = @Email, Designation = @Designation,
                IsPrimary = @IsPrimary, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE CustomerContactId = @CustomerContactId AND CustomerId = @CustomerId AND IsDeleted = 0;

            IF @@ROWCOUNT = 0
                THROW 50404, N'CONTACT_NOT_FOUND|Contact was not found.', 1;
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @CustomerContactId AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_CustomerContact_Delete
    @CustomerContactId  BIGINT,
    @CustomerId         BIGINT,
    @UserId             BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE core.CustomerContact
    SET IsDeleted = 1, ModifiedBy = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE CustomerContactId = @CustomerContactId AND CustomerId = @CustomerId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0
        THROW 50404, N'CONTACT_NOT_FOUND|Contact was not found.', 1;
END
GO
