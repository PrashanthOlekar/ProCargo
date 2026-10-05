/*
    Support (complaints, tickets, public enquiries), notifications and audit log.
*/

IF OBJECT_ID(N'sup.Complaint', N'U') IS NULL
BEGIN
    CREATE TABLE sup.Complaint
    (
        ComplaintId        BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Complaint PRIMARY KEY,
        ComplaintNumber    VARCHAR(30)     NOT NULL CONSTRAINT UQ_Complaint_ComplaintNumber UNIQUE,
        RaisedByUserId     BIGINT          NOT NULL,
        BookingId          BIGINT          NULL,
        TripId             BIGINT          NULL,
        Category           VARCHAR(30)     NOT NULL CONSTRAINT CK_Complaint_Category CHECK (Category IN ('Delay','Damage','Behaviour','Billing','Payment','Other')),
        Subject            NVARCHAR(200)   NOT NULL,
        Description        NVARCHAR(2000)  NOT NULL,
        ComplaintStatusId  INT             NOT NULL,
        AssignedToUserId   BIGINT          NULL,
        Resolution         NVARCHAR(2000)  NULL,
        ResolvedDateUtc    DATETIME2(3)    NULL,
        CreatedBy          BIGINT          NULL,
        CreatedDateUtc     DATETIME2(3)    NOT NULL CONSTRAINT DF_Complaint_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT          NULL,
        ModifiedDateUtc    DATETIME2(3)    NULL,
        RowVersion         ROWVERSION      NOT NULL
    );
END
GO

IF OBJECT_ID(N'sup.SupportTicket', N'U') IS NULL
BEGIN
    CREATE TABLE sup.SupportTicket
    (
        SupportTicketId    BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupportTicket PRIMARY KEY,
        TicketNumber       VARCHAR(30)     NOT NULL CONSTRAINT UQ_SupportTicket_TicketNumber UNIQUE,
        RaisedByUserId     BIGINT          NOT NULL,
        BookingId          BIGINT          NULL,
        Subject            NVARCHAR(200)   NOT NULL,
        Description        NVARCHAR(2000)  NOT NULL,
        TicketPriorityId   INT             NOT NULL,
        TicketStatusId     INT             NOT NULL,
        AssignedToUserId   BIGINT          NULL,
        ClosedDateUtc      DATETIME2(3)    NULL,
        CreatedBy          BIGINT          NULL,
        CreatedDateUtc     DATETIME2(3)    NOT NULL CONSTRAINT DF_SupportTicket_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy         BIGINT          NULL,
        ModifiedDateUtc    DATETIME2(3)    NULL,
        RowVersion         ROWVERSION      NOT NULL
    );
END
GO

IF OBJECT_ID(N'sup.SupportTicketComment', N'U') IS NULL
BEGIN
    CREATE TABLE sup.SupportTicketComment
    (
        SupportTicketCommentId BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupportTicketComment PRIMARY KEY,
        SupportTicketId        BIGINT          NOT NULL,
        CommentText            NVARCHAR(2000)  NOT NULL,
        IsInternal             BIT             NOT NULL CONSTRAINT DF_SupportTicketComment_IsInternal DEFAULT (0),
        CreatedBy              BIGINT          NOT NULL,
        CreatedDateUtc         DATETIME2(3)    NOT NULL CONSTRAINT DF_SupportTicketComment_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'sup.ContactEnquiry', N'U') IS NULL
BEGIN
    CREATE TABLE sup.ContactEnquiry
    (
        ContactEnquiryId  BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_ContactEnquiry PRIMARY KEY,
        FullName          NVARCHAR(150)   NOT NULL,
        Email             NVARCHAR(256)   NOT NULL,
        PhoneNumber       VARCHAR(20)     NULL,
        Subject           NVARCHAR(200)   NOT NULL,
        Message           NVARCHAR(2000)  NOT NULL,
        IpAddress         VARCHAR(45)     NULL,
        IsHandled         BIT             NOT NULL CONSTRAINT DF_ContactEnquiry_IsHandled DEFAULT (0),
        HandledBy         BIGINT          NULL,
        HandledDateUtc    DATETIME2(3)    NULL,
        CreatedDateUtc    DATETIME2(3)    NOT NULL CONSTRAINT DF_ContactEnquiry_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'sup.NotificationTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE sup.NotificationTemplate
    (
        NotificationTemplateId  INT             IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationTemplate PRIMARY KEY,
        TemplateCode            VARCHAR(60)     NOT NULL,
        NotificationChannelId   INT             NOT NULL,
        Subject                 NVARCHAR(200)   NOT NULL,
        Body                    NVARCHAR(4000)  NOT NULL,
        IsActive                BIT             NOT NULL CONSTRAINT DF_NotificationTemplate_IsActive DEFAULT (1),
        CreatedBy               BIGINT          NULL,
        CreatedDateUtc          DATETIME2(3)    NOT NULL CONSTRAINT DF_NotificationTemplate_CreatedDateUtc DEFAULT (SYSUTCDATETIME()),
        ModifiedBy              BIGINT          NULL,
        ModifiedDateUtc         DATETIME2(3)    NULL,
        CONSTRAINT UQ_NotificationTemplate_Code_Channel UNIQUE (TemplateCode, NotificationChannelId)
    );
END
GO

IF OBJECT_ID(N'sup.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE sup.Notification
    (
        NotificationId         BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notification PRIMARY KEY,
        UserId                 BIGINT          NOT NULL,
        NotificationChannelId  INT             NOT NULL,
        TemplateCode           VARCHAR(60)     NOT NULL,
        Title                  NVARCHAR(200)   NOT NULL,
        Message                NVARCHAR(2000)  NOT NULL,
        NotificationStatusId   INT             NOT NULL,
        EntityType             VARCHAR(30)     NULL,
        EntityId               BIGINT          NULL,
        FailureReason          NVARCHAR(500)   NULL,
        SentDateUtc            DATETIME2(3)    NULL,
        ReadDateUtc            DATETIME2(3)    NULL,
        CreatedDateUtc         DATETIME2(3)    NOT NULL CONSTRAINT DF_Notification_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'aud.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE aud.AuditLog
    (
        AuditLogId      BIGINT          IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
        UserId          BIGINT          NULL,
        Action          VARCHAR(100)    NOT NULL,
        EntityType      VARCHAR(50)     NOT NULL,
        EntityId        VARCHAR(50)     NULL,
        OldValue        NVARCHAR(4000)  NULL,
        NewValue        NVARCHAR(4000)  NULL,
        IpAddress       VARCHAR(45)     NULL,
        UserAgent       NVARCHAR(300)   NULL,
        TraceId         VARCHAR(64)     NULL,
        CreatedDateUtc  DATETIME2(3)    NOT NULL CONSTRAINT DF_AuditLog_CreatedDateUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO
