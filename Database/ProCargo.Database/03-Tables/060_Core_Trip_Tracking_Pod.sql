/*
    Trip, assignment history, status history, tracking, OTP verification and proof of delivery.

    Design notes
    * Trip.VehicleId / Trip.DriverId hold the CURRENT assignment; TripAssignment keeps the full history
      so reassignments remain auditable.
    * Trip.OwnerId is denormalised from the vehicle at assignment time. It makes the very frequent
      "trips of this owner" query a single indexed lookup and freezes ownership for settlement.
    * Pickup and delivery OTP verifications share one table (TripVerification) distinguished by
      VerificationTypeId (1 Pickup, 2 Delivery) because their columns and rules are identical.
    * Tracking is provider-independent: every point records the TrackingProvider it came from.
*/

IF OBJECT_ID(N'core.Trip', N'U') IS NULL
BEGIN
    CREATE TABLE core.Trip
    (
        TripId                  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_Trip PRIMARY KEY,
        TripNumber              VARCHAR(30)    NOT NULL CONSTRAINT UQ_Trip_TripNumber UNIQUE,
        BookingId               BIGINT         NOT NULL,
        QuotationId             BIGINT         NOT NULL,
        VehicleId               BIGINT         NOT NULL,
        DriverId                BIGINT         NOT NULL,
        OwnerId                 BIGINT         NOT NULL,
        TripStatusId            INT            NOT NULL,
        StatusBeforeHoldId      INT            NULL,
        PlannedPickupDateUtc    DATETIME2(3)   NOT NULL,
        ActualPickupDateUtc     DATETIME2(3)   NULL,
        PlannedDeliveryDateUtc  DATETIME2(3)   NOT NULL,
        ActualDeliveryDateUtc   DATETIME2(3)   NULL,
        StartOdometer           INT            NULL CONSTRAINT CK_Trip_StartOdometer CHECK (StartOdometer IS NULL OR StartOdometer >= 0),
        EndOdometer             INT            NULL,
        ExceptionReason         NVARCHAR(500)  NULL,
        CancellationReason      NVARCHAR(500)  NULL,
        CreatedBy               BIGINT         NULL,
        CreatedDateUtc          DATETIME2(3)   NOT NULL CONSTRAINT DF_Trip_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy              BIGINT         NULL,
        ModifiedDateUtc         DATETIME2(3)   NULL,
        RowVersion              ROWVERSION     NOT NULL,
        CONSTRAINT CK_Trip_PlannedDates CHECK (PlannedDeliveryDateUtc > PlannedPickupDateUtc),
        CONSTRAINT CK_Trip_Odometer CHECK (EndOdometer IS NULL OR StartOdometer IS NULL OR EndOdometer >= StartOdometer)
    );
END
GO

IF OBJECT_ID(N'core.TripAssignment', N'U') IS NULL
BEGIN
    CREATE TABLE core.TripAssignment
    (
        TripAssignmentId   BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripAssignment PRIMARY KEY,
        TripId             BIGINT         NOT NULL,
        AssignmentTypeId   INT            NOT NULL CONSTRAINT CK_TripAssignment_AssignmentTypeId CHECK (AssignmentTypeId IN (1, 2)), -- 1 Vehicle, 2 Driver
        VehicleId          BIGINT         NULL,
        DriverId           BIGINT         NULL,
        AssignedBy         BIGINT         NOT NULL,
        AssignedDateUtc    DATETIME2(3)   NOT NULL CONSTRAINT DF_TripAssignment_AssignedDateUtc DEFAULT (SYSUTCDATETIME()),
        ReleasedDateUtc    DATETIME2(3)   NULL,
        Reason             NVARCHAR(300)  NULL,
        CONSTRAINT CK_TripAssignment_Target CHECK
        (
            (AssignmentTypeId = 1 AND VehicleId IS NOT NULL AND DriverId IS NULL) OR
            (AssignmentTypeId = 2 AND DriverId IS NOT NULL AND VehicleId IS NULL)
        )
    );
END
GO

IF OBJECT_ID(N'core.TripStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.TripStatusHistory
    (
        TripStatusHistoryId BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripStatusHistory PRIMARY KEY,
        TripId              BIGINT         NOT NULL,
        FromStatusId        INT            NULL,
        ToStatusId          INT            NOT NULL,
        Remarks             NVARCHAR(500)  NULL,
        ChangedBy           BIGINT         NULL,
        ChangedDateUtc      DATETIME2(3)   NOT NULL CONSTRAINT DF_TripStatusHistory_ChangedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.TrackingProvider', N'U') IS NULL
BEGIN
    CREATE TABLE core.TrackingProvider
    (
        TrackingProviderId INT            NOT NULL CONSTRAINT PK_TrackingProvider PRIMARY KEY,
        Code               VARCHAR(40)    NOT NULL CONSTRAINT UQ_TrackingProvider_Code UNIQUE,
        Name               NVARCHAR(100)  NOT NULL,
        IsActive           BIT            NOT NULL CONSTRAINT DF_TrackingProvider_IsActive DEFAULT (1)
    );
END
GO

IF OBJECT_ID(N'core.TripLocationHistory', N'U') IS NULL
BEGIN
    CREATE TABLE core.TripLocationHistory
    (
        TripLocationHistoryId BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripLocationHistory PRIMARY KEY,
        TripId                BIGINT         NOT NULL,
        Latitude              DECIMAL(9,6)   NOT NULL CONSTRAINT CK_TripLocationHistory_Latitude CHECK (Latitude BETWEEN -90 AND 90),
        Longitude             DECIMAL(9,6)   NOT NULL CONSTRAINT CK_TripLocationHistory_Longitude CHECK (Longitude BETWEEN -180 AND 180),
        RecordedDateUtc       DATETIME2(3)   NOT NULL,
        ReceivedDateUtc       DATETIME2(3)   NOT NULL CONSTRAINT DF_TripLocationHistory_ReceivedDateUtc DEFAULT (SYSUTCDATETIME()),
        TrackingProviderId    INT            NOT NULL,
        Accuracy              DECIMAL(8,2)   NULL CONSTRAINT CK_TripLocationHistory_Accuracy CHECK (Accuracy IS NULL OR Accuracy >= 0),
        SpeedKmph             DECIMAL(6,2)   NULL CONSTRAINT CK_TripLocationHistory_Speed CHECK (SpeedKmph IS NULL OR SpeedKmph >= 0),
        Heading               DECIMAL(5,2)   NULL CONSTRAINT CK_TripLocationHistory_Heading CHECK (Heading IS NULL OR Heading BETWEEN 0 AND 360)
    );
END
GO

IF OBJECT_ID(N'core.TripVerification', N'U') IS NULL
BEGIN
    CREATE TABLE core.TripVerification
    (
        TripVerificationId  BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_TripVerification PRIMARY KEY,
        TripId              BIGINT         NOT NULL,
        VerificationTypeId  INT            NOT NULL CONSTRAINT CK_TripVerification_Type CHECK (VerificationTypeId IN (1, 2)), -- 1 Pickup, 2 Delivery
        OtpHash             BINARY(32)     NOT NULL,
        SentToPhoneMasked   VARCHAR(20)    NOT NULL,
        ExpiresDateUtc      DATETIME2(3)   NOT NULL,
        AttemptCount        INT            NOT NULL CONSTRAINT DF_TripVerification_AttemptCount DEFAULT (0),
        MaxAttempts         INT            NOT NULL CONSTRAINT DF_TripVerification_MaxAttempts DEFAULT (5),
        IsVerified          BIT            NOT NULL CONSTRAINT DF_TripVerification_IsVerified DEFAULT (0),
        IsSuperseded        BIT            NOT NULL CONSTRAINT DF_TripVerification_IsSuperseded DEFAULT (0),
        VerifiedDateUtc     DATETIME2(3)   NULL,
        VerifiedBy          BIGINT         NULL,
        ReceiverName        NVARCHAR(150)  NULL,
        Latitude            DECIMAL(9,6)   NULL,
        Longitude           DECIMAL(9,6)   NULL,
        CreatedBy           BIGINT         NULL,
        CreatedDateUtc      DATETIME2(3)   NOT NULL CONSTRAINT DF_TripVerification_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_TripVerification_Attempts CHECK (AttemptCount >= 0 AND MaxAttempts > 0)
    );
END
GO

IF OBJECT_ID(N'core.ProofOfDelivery', N'U') IS NULL
BEGIN
    CREATE TABLE core.ProofOfDelivery
    (
        ProofOfDeliveryId        BIGINT         IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofOfDelivery PRIMARY KEY,
        TripId                   BIGINT         NOT NULL CONSTRAINT UQ_ProofOfDelivery_TripId UNIQUE,
        ReceiverName             NVARCHAR(150)  NOT NULL,
        ReceiverPhone            VARCHAR(20)    NULL,
        DeliveredDateUtc         DATETIME2(3)   NOT NULL,
        Remarks                  NVARCHAR(1000) NULL,
        Latitude                 DECIMAL(9,6)   NULL,
        Longitude                DECIMAL(9,6)   NULL,
        SignatureFileId          BIGINT         NULL,
        SignatureCapturedDateUtc DATETIME2(3)   NULL,
        UploadedBy               BIGINT         NOT NULL,
        CreatedDateUtc           DATETIME2(3)   NOT NULL CONSTRAINT DF_ProofOfDelivery_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'core.ProofOfDeliveryFile', N'U') IS NULL
BEGIN
    CREATE TABLE core.ProofOfDeliveryFile
    (
        ProofOfDeliveryFileId BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofOfDeliveryFile PRIMARY KEY,
        ProofOfDeliveryId     BIGINT        NOT NULL,
        StoredFileId          BIGINT        NOT NULL,
        FileCategory          VARCHAR(20)   NOT NULL CONSTRAINT CK_ProofOfDeliveryFile_Category CHECK (FileCategory IN ('Photo','Document')),
        CreatedDateUtc        DATETIME2(3)  NOT NULL CONSTRAINT DF_ProofOfDeliveryFile_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
