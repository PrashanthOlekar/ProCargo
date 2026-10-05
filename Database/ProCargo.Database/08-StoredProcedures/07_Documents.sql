/*
    Module: Stored files and KYC/compliance documents (schema core)

    Documents live in four tables (CustomerDocument, OwnerDocument, DriverDocument, VehicleDocument) so each
    has a real foreign key to its parent. The procedures below expose one consistent API over the four tables
    using a static IF/ELSE on @EntityType - no dynamic SQL is used.
*/

CREATE OR ALTER PROCEDURE core.usp_StoredFile_Create
    @StorageKey        VARCHAR(400),
    @OriginalFileName  NVARCHAR(255),
    @ContentType       VARCHAR(100),
    @SizeBytes         BIGINT,
    @Sha256Hash        BINARY(32),
    @UploadedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO core.StoredFile (StorageKey, OriginalFileName, ContentType, SizeBytes, Sha256Hash, UploadedBy)
    VALUES (@StorageKey, @OriginalFileName, @ContentType, @SizeBytes, @Sha256Hash, @UploadedBy);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_StoredFile_GetById
    @StoredFileId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT sf.StoredFileId, sf.StorageKey, sf.OriginalFileName, sf.ContentType, sf.SizeBytes, sf.UploadedBy, sf.CreatedDateUtc
    FROM core.StoredFile AS sf
    WHERE sf.StoredFileId = @StoredFileId AND sf.IsDeleted = 0;
END
GO

CREATE OR ALTER PROCEDURE core.usp_EntityDocument_Create
    @EntityType      VARCHAR(20),   -- Customer | Owner | Driver | Vehicle
    @EntityId        BIGINT,
    @DocumentTypeId  INT,
    @StoredFileId    BIGINT,
    @DocumentNumber  NVARCHAR(50),
    @ExpiryDate      DATE,
    @CreatedBy       BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AppliesTo VARCHAR(20), @RequiresExpiry BIT;
    SELECT @AppliesTo = AppliesTo, @RequiresExpiry = RequiresExpiry
    FROM mst.DocumentType WHERE DocumentTypeId = @DocumentTypeId AND IsActive = 1;

    IF @AppliesTo IS NULL OR @AppliesTo <> @EntityType
        THROW 50400, N'DOCUMENT_TYPE_INVALID|The document type does not apply to this record.', 1;

    IF @RequiresExpiry = 1 AND @ExpiryDate IS NULL
        THROW 50400, N'DOCUMENT_EXPIRY_REQUIRED|An expiry date is required for this document type.', 1;

    IF @ExpiryDate IS NOT NULL AND @ExpiryDate < CAST(SYSUTCDATETIME() AS DATE)
        THROW 50400, N'DOCUMENT_EXPIRED|The document has already expired.', 1;

    DECLARE @Id BIGINT;

    IF @EntityType = 'Customer'
    BEGIN
        INSERT INTO core.CustomerDocument (CustomerId, DocumentTypeId, StoredFileId, DocumentNumber, ExpiryDate, CreatedBy)
        VALUES (@EntityId, @DocumentTypeId, @StoredFileId, @DocumentNumber, @ExpiryDate, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE IF @EntityType = 'Owner'
    BEGIN
        INSERT INTO core.OwnerDocument (OwnerId, DocumentTypeId, StoredFileId, DocumentNumber, ExpiryDate, CreatedBy)
        VALUES (@EntityId, @DocumentTypeId, @StoredFileId, @DocumentNumber, @ExpiryDate, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE IF @EntityType = 'Driver'
    BEGIN
        INSERT INTO core.DriverDocument (DriverId, DocumentTypeId, StoredFileId, DocumentNumber, ExpiryDate, CreatedBy)
        VALUES (@EntityId, @DocumentTypeId, @StoredFileId, @DocumentNumber, @ExpiryDate, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE IF @EntityType = 'Vehicle'
    BEGIN
        INSERT INTO core.VehicleDocument (VehicleId, DocumentTypeId, StoredFileId, DocumentNumber, ExpiryDate, CreatedBy)
        VALUES (@EntityId, @DocumentTypeId, @StoredFileId, @DocumentNumber, @ExpiryDate, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();
    END
    ELSE
        THROW 50400, N'ENTITY_TYPE_INVALID|Unsupported document owner type.', 1;

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE core.usp_EntityDocument_Get
    @EntityType  VARCHAR(20),
    @EntityId    BIGINT,
    @DocumentId  BIGINT = NULL      -- NULL => all documents of the entity
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH docs AS
    (
        SELECT 'Customer' AS EntityType, d.CustomerDocumentId AS DocumentId, d.CustomerId AS EntityId, d.DocumentTypeId, d.StoredFileId,
               d.DocumentNumber, d.ExpiryDate, d.VerificationStatusId, d.Remarks, d.VerifiedDateUtc, d.CreatedDateUtc
        FROM core.CustomerDocument AS d WHERE @EntityType = 'Customer' AND d.CustomerId = @EntityId AND d.IsDeleted = 0
        UNION ALL
        SELECT 'Owner', d.OwnerDocumentId, d.OwnerId, d.DocumentTypeId, d.StoredFileId,
               d.DocumentNumber, d.ExpiryDate, d.VerificationStatusId, d.Remarks, d.VerifiedDateUtc, d.CreatedDateUtc
        FROM core.OwnerDocument AS d WHERE @EntityType = 'Owner' AND d.OwnerId = @EntityId AND d.IsDeleted = 0
        UNION ALL
        SELECT 'Driver', d.DriverDocumentId, d.DriverId, d.DocumentTypeId, d.StoredFileId,
               d.DocumentNumber, d.ExpiryDate, d.VerificationStatusId, d.Remarks, d.VerifiedDateUtc, d.CreatedDateUtc
        FROM core.DriverDocument AS d WHERE @EntityType = 'Driver' AND d.DriverId = @EntityId AND d.IsDeleted = 0
        UNION ALL
        SELECT 'Vehicle', d.VehicleDocumentId, d.VehicleId, d.DocumentTypeId, d.StoredFileId,
               d.DocumentNumber, d.ExpiryDate, d.VerificationStatusId, d.Remarks, d.VerifiedDateUtc, d.CreatedDateUtc
        FROM core.VehicleDocument AS d WHERE @EntityType = 'Vehicle' AND d.VehicleId = @EntityId AND d.IsDeleted = 0
    )
    SELECT
        docs.EntityType,
        docs.DocumentId,
        docs.EntityId,
        docs.DocumentTypeId,
        dt.Code AS DocumentTypeCode,
        dt.Name AS DocumentTypeName,
        docs.StoredFileId,
        sf.OriginalFileName,
        sf.ContentType,
        sf.SizeBytes,
        sf.StorageKey,
        docs.DocumentNumber,
        docs.ExpiryDate,
        docs.VerificationStatusId,
        docs.Remarks,
        docs.VerifiedDateUtc,
        docs.CreatedDateUtc
    FROM docs
    INNER JOIN mst.DocumentType AS dt ON dt.DocumentTypeId = docs.DocumentTypeId
    INNER JOIN core.StoredFile  AS sf ON sf.StoredFileId = docs.StoredFileId
    WHERE @DocumentId IS NULL OR docs.DocumentId = @DocumentId
    ORDER BY docs.CreatedDateUtc DESC
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE core.usp_EntityDocument_SetVerification
    @EntityType            VARCHAR(20),
    @EntityId              BIGINT,
    @DocumentId            BIGINT,
    @VerificationStatusId  INT,
    @Remarks               NVARCHAR(500),
    @VerifiedBy            BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Rows INT = 0;

    IF @EntityType = 'Customer'
    BEGIN
        UPDATE core.CustomerDocument SET VerificationStatusId = @VerificationStatusId, Remarks = @Remarks, VerifiedBy = @VerifiedBy, VerifiedDateUtc = @Now, ModifiedBy = @VerifiedBy, ModifiedDateUtc = @Now
            WHERE CustomerDocumentId = @DocumentId AND CustomerId = @EntityId AND IsDeleted = 0;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Owner'
    BEGIN
        UPDATE core.OwnerDocument SET VerificationStatusId = @VerificationStatusId, Remarks = @Remarks, VerifiedBy = @VerifiedBy, VerifiedDateUtc = @Now, ModifiedBy = @VerifiedBy, ModifiedDateUtc = @Now
            WHERE OwnerDocumentId = @DocumentId AND OwnerId = @EntityId AND IsDeleted = 0;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Driver'
    BEGIN
        UPDATE core.DriverDocument SET VerificationStatusId = @VerificationStatusId, Remarks = @Remarks, VerifiedBy = @VerifiedBy, VerifiedDateUtc = @Now, ModifiedBy = @VerifiedBy, ModifiedDateUtc = @Now
            WHERE DriverDocumentId = @DocumentId AND DriverId = @EntityId AND IsDeleted = 0;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Vehicle'
    BEGIN
        UPDATE core.VehicleDocument SET VerificationStatusId = @VerificationStatusId, Remarks = @Remarks, VerifiedBy = @VerifiedBy, VerifiedDateUtc = @Now, ModifiedBy = @VerifiedBy, ModifiedDateUtc = @Now
            WHERE VehicleDocumentId = @DocumentId AND VehicleId = @EntityId AND IsDeleted = 0;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE
        THROW 50400, N'ENTITY_TYPE_INVALID|Unsupported document owner type.', 1;

    IF @Rows = 0
        THROW 50404, N'DOCUMENT_NOT_FOUND|Document was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE core.usp_EntityDocument_Delete
    @EntityType  VARCHAR(20),
    @EntityId    BIGINT,
    @DocumentId  BIGINT,
    @ModifiedBy  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Rows INT = 0;

    -- verified documents are evidence and cannot be removed by the uploader
    IF @EntityType = 'Customer'
    BEGIN
        UPDATE core.CustomerDocument SET IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
            WHERE CustomerDocumentId = @DocumentId AND CustomerId = @EntityId AND IsDeleted = 0 AND VerificationStatusId <> 3;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Owner'
    BEGIN
        UPDATE core.OwnerDocument SET IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
            WHERE OwnerDocumentId = @DocumentId AND OwnerId = @EntityId AND IsDeleted = 0 AND VerificationStatusId <> 3;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Driver'
    BEGIN
        UPDATE core.DriverDocument SET IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
            WHERE DriverDocumentId = @DocumentId AND DriverId = @EntityId AND IsDeleted = 0 AND VerificationStatusId <> 3;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE IF @EntityType = 'Vehicle'
    BEGIN
        UPDATE core.VehicleDocument SET IsDeleted = 1, ModifiedBy = @ModifiedBy, ModifiedDateUtc = @Now
            WHERE VehicleDocumentId = @DocumentId AND VehicleId = @EntityId AND IsDeleted = 0 AND VerificationStatusId <> 3;
        SET @Rows = @@ROWCOUNT;
    END
    ELSE
        THROW 50400, N'ENTITY_TYPE_INVALID|Unsupported document owner type.', 1;

    IF @Rows = 0
        THROW 50404, N'DOCUMENT_NOT_FOUND|Document was not found or is already verified.', 1;
END
GO
