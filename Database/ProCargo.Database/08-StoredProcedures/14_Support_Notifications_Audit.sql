/*
    Module: Complaints, support tickets, contact enquiries, notifications and audit (schemas sup, aud)
    Complaint status: 1 Open, 2 Assigned, 3 Investigating, 4 Resolved, 5 Rejected, 6 Closed
    Ticket status   : 1 Open, 2 InProgress, 3 WaitingOnCustomer, 4 Resolved, 5 Closed
*/

CREATE OR ALTER PROCEDURE sup.usp_Complaint_Create
    @RaisedByUserId  BIGINT,
    @BookingId       BIGINT,
    @TripId          BIGINT,
    @Category        VARCHAR(30),
    @Subject         NVARCHAR(200),
    @Description     NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Number VARCHAR(30) = core.fn_FormatBusinessNumber('CMP', NEXT VALUE FOR sup.seq_ComplaintNumber, @Now);

    INSERT INTO sup.Complaint (ComplaintNumber, RaisedByUserId, BookingId, TripId, Category, Subject, Description, ComplaintStatusId, CreatedBy, CreatedDateUtc)
    VALUES (@Number, @RaisedByUserId, @BookingId, @TripId, @Category, @Subject, @Description, 1, @RaisedByUserId, @Now);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id, @Number AS Number;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Complaint_GetById
    @ComplaintId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ComplaintId, c.ComplaintNumber, c.RaisedByUserId, ru.FullName AS RaisedByName, c.BookingId, b.BookingNumber,
           c.TripId, t.TripNumber, c.Category, c.Subject, c.Description, c.ComplaintStatusId, c.AssignedToUserId,
           au.FullName AS AssignedToName, c.Resolution, c.ResolvedDateUtc, c.CreatedDateUtc, c.RowVersion
    FROM sup.Complaint AS c
    INNER JOIN sec.[User]   AS ru ON ru.UserId = c.RaisedByUserId
    LEFT  JOIN sec.[User]   AS au ON au.UserId = c.AssignedToUserId
    LEFT  JOIN core.Booking AS b  ON b.BookingId = c.BookingId
    LEFT  JOIN core.Trip    AS t  ON t.TripId = c.TripId
    WHERE c.ComplaintId = @ComplaintId;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Complaint_GetPaged
    @PageNumber         INT,
    @PageSize           INT,
    @Search             NVARCHAR(100) = NULL,
    @RaisedByUserId     BIGINT        = NULL,
    @AssignedToUserId   BIGINT        = NULL,
    @ComplaintStatusId  INT           = NULL,
    @Category           VARCHAR(30)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.ComplaintId, c.ComplaintNumber, ru.FullName AS RaisedByName, c.Category, c.Subject, c.ComplaintStatusId,
           au.FullName AS AssignedToName, c.CreatedDateUtc, COUNT(*) OVER () AS TotalRecords
    FROM sup.Complaint AS c
    INNER JOIN sec.[User] AS ru ON ru.UserId = c.RaisedByUserId
    LEFT  JOIN sec.[User] AS au ON au.UserId = c.AssignedToUserId
    WHERE (@RaisedByUserId IS NULL OR c.RaisedByUserId = @RaisedByUserId)
      AND (@AssignedToUserId IS NULL OR c.AssignedToUserId = @AssignedToUserId)
      AND (@ComplaintStatusId IS NULL OR c.ComplaintStatusId = @ComplaintStatusId)
      AND (@Category IS NULL OR c.Category = @Category)
      AND (@Search IS NULL OR c.ComplaintNumber LIKE @Search + '%' OR c.Subject LIKE N'%' + @Search + N'%')
    ORDER BY c.CreatedDateUtc DESC, c.ComplaintId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Complaint_Assign
    @ComplaintId       BIGINT,
    @AssignedToUserId  BIGINT,
    @ModifiedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM sec.[User] WHERE UserId = @AssignedToUserId AND IsInternal = 1 AND IsActive = 1 AND IsDeleted = 0)
        THROW 50400, N'ASSIGNEE_INVALID|Complaints can only be assigned to active internal users.', 1;

    UPDATE sup.Complaint
    SET AssignedToUserId = @AssignedToUserId,
        ComplaintStatusId = CASE WHEN ComplaintStatusId = 1 THEN 2 ELSE ComplaintStatusId END,
        ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE ComplaintId = @ComplaintId AND ComplaintStatusId IN (1, 2, 3);

    IF @@ROWCOUNT = 0
        THROW 50400, N'COMPLAINT_NOT_ASSIGNABLE|Only open complaints can be assigned.', 1;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Complaint_ChangeStatus
    @ComplaintId       BIGINT,
    @ExpectedStatusId  INT,
    @NewStatusId       INT,
    @Resolution        NVARCHAR(2000),
    @ModifiedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sup.Complaint
    SET ComplaintStatusId = @NewStatusId,
        Resolution = CASE WHEN @Resolution IS NOT NULL THEN @Resolution ELSE Resolution END,
        ResolvedDateUtc = CASE WHEN @NewStatusId IN (4, 5) THEN SYSUTCDATETIME() ELSE ResolvedDateUtc END,
        ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE ComplaintId = @ComplaintId AND ComplaintStatusId = @ExpectedStatusId;

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sup.Complaint WHERE ComplaintId = @ComplaintId)
            THROW 50404, N'COMPLAINT_NOT_FOUND|Complaint was not found.', 1;
        THROW 50409, N'COMPLAINT_STATUS_CHANGED|The complaint status was changed by another request.', 1;
    END
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicket_Create
    @RaisedByUserId    BIGINT,
    @BookingId         BIGINT,
    @Subject           NVARCHAR(200),
    @Description       NVARCHAR(2000),
    @TicketPriorityId  INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2(3) = SYSUTCDATETIME();
    DECLARE @Number VARCHAR(30) = core.fn_FormatBusinessNumber('TKT', NEXT VALUE FOR sup.seq_TicketNumber, @Now);

    INSERT INTO sup.SupportTicket (TicketNumber, RaisedByUserId, BookingId, Subject, Description, TicketPriorityId, TicketStatusId, CreatedBy, CreatedDateUtc)
    VALUES (@Number, @RaisedByUserId, @BookingId, @Subject, @Description, @TicketPriorityId, 1, @RaisedByUserId, @Now);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id, @Number AS Number;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicket_GetById
    @SupportTicketId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.SupportTicketId, st.TicketNumber, st.RaisedByUserId, ru.FullName AS RaisedByName, st.BookingId, b.BookingNumber,
           st.Subject, st.Description, st.TicketPriorityId, st.TicketStatusId, st.AssignedToUserId, au.FullName AS AssignedToName,
           st.ClosedDateUtc, st.CreatedDateUtc, st.RowVersion
    FROM sup.SupportTicket AS st
    INNER JOIN sec.[User]   AS ru ON ru.UserId = st.RaisedByUserId
    LEFT  JOIN sec.[User]   AS au ON au.UserId = st.AssignedToUserId
    LEFT  JOIN core.Booking AS b  ON b.BookingId = st.BookingId
    WHERE st.SupportTicketId = @SupportTicketId;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicket_GetPaged
    @PageNumber        INT,
    @PageSize          INT,
    @Search            NVARCHAR(100) = NULL,
    @RaisedByUserId    BIGINT        = NULL,
    @AssignedToUserId  BIGINT        = NULL,
    @TicketStatusId    INT           = NULL,
    @TicketPriorityId  INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.SupportTicketId, st.TicketNumber, ru.FullName AS RaisedByName, st.Subject, st.TicketPriorityId, st.TicketStatusId,
           au.FullName AS AssignedToName, st.CreatedDateUtc, COUNT(*) OVER () AS TotalRecords
    FROM sup.SupportTicket AS st
    INNER JOIN sec.[User] AS ru ON ru.UserId = st.RaisedByUserId
    LEFT  JOIN sec.[User] AS au ON au.UserId = st.AssignedToUserId
    WHERE (@RaisedByUserId IS NULL OR st.RaisedByUserId = @RaisedByUserId)
      AND (@AssignedToUserId IS NULL OR st.AssignedToUserId = @AssignedToUserId)
      AND (@TicketStatusId IS NULL OR st.TicketStatusId = @TicketStatusId)
      AND (@TicketPriorityId IS NULL OR st.TicketPriorityId = @TicketPriorityId)
      AND (@Search IS NULL OR st.TicketNumber LIKE @Search + '%' OR st.Subject LIKE N'%' + @Search + N'%')
    ORDER BY st.TicketPriorityId DESC, st.CreatedDateUtc DESC, st.SupportTicketId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicket_Update
    @SupportTicketId   BIGINT,
    @AssignedToUserId  BIGINT,
    @TicketStatusId    INT,
    @TicketPriorityId  INT,
    @ModifiedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF @AssignedToUserId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sec.[User] WHERE UserId = @AssignedToUserId AND IsInternal = 1 AND IsActive = 1 AND IsDeleted = 0)
        THROW 50400, N'ASSIGNEE_INVALID|Tickets can only be assigned to active internal users.', 1;

    UPDATE sup.SupportTicket
    SET AssignedToUserId = ISNULL(@AssignedToUserId, AssignedToUserId),
        TicketStatusId = ISNULL(@TicketStatusId, TicketStatusId),
        TicketPriorityId = ISNULL(@TicketPriorityId, TicketPriorityId),
        ClosedDateUtc = CASE WHEN @TicketStatusId = 5 THEN SYSUTCDATETIME() WHEN @TicketStatusId IS NOT NULL THEN NULL ELSE ClosedDateUtc END,
        ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SupportTicketId = @SupportTicketId;

    IF @@ROWCOUNT = 0
        THROW 50404, N'TICKET_NOT_FOUND|Support ticket was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicketComment_Create
    @SupportTicketId  BIGINT,
    @CommentText      NVARCHAR(2000),
    @IsInternal       BIT,
    @CreatedBy        BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Id BIGINT;

    BEGIN TRANSACTION;
        INSERT INTO sup.SupportTicketComment (SupportTicketId, CommentText, IsInternal, CreatedBy)
        VALUES (@SupportTicketId, @CommentText, @IsInternal, @CreatedBy);
        SET @Id = SCOPE_IDENTITY();

        -- a reply from the requester re-opens a ticket that was waiting on them
        UPDATE sup.SupportTicket SET TicketStatusId = 2, ModifiedBy = @CreatedBy, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE SupportTicketId = @SupportTicketId AND RaisedByUserId = @CreatedBy AND TicketStatusId IN (3, 4);
    COMMIT TRANSACTION;

    SELECT @Id AS Id;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_SupportTicketComment_GetByTicket
    @SupportTicketId  BIGINT,
    @IncludeInternal  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.SupportTicketCommentId, c.CommentText, c.IsInternal, c.CreatedBy, u.FullName AS CreatedByName, u.IsInternal AS IsStaff, c.CreatedDateUtc
    FROM sup.SupportTicketComment AS c
    INNER JOIN sec.[User] AS u ON u.UserId = c.CreatedBy
    WHERE c.SupportTicketId = @SupportTicketId AND (@IncludeInternal = 1 OR c.IsInternal = 0)
    ORDER BY c.CreatedDateUtc, c.SupportTicketCommentId;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_ContactEnquiry_Create
    @FullName     NVARCHAR(150),
    @Email        NVARCHAR(256),
    @PhoneNumber  VARCHAR(20),
    @Subject      NVARCHAR(200),
    @Message      NVARCHAR(2000),
    @IpAddress    VARCHAR(45)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO sup.ContactEnquiry (FullName, Email, PhoneNumber, Subject, Message, IpAddress)
    VALUES (@FullName, @Email, @PhoneNumber, @Subject, @Message, @IpAddress);
    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_ContactEnquiry_GetPaged
    @PageNumber  INT,
    @PageSize    INT,
    @IsHandled   BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ce.ContactEnquiryId, ce.FullName, ce.Email, ce.PhoneNumber, ce.Subject, ce.Message, ce.IsHandled, ce.HandledDateUtc,
           ce.CreatedDateUtc, COUNT(*) OVER () AS TotalRecords
    FROM sup.ContactEnquiry AS ce
    WHERE @IsHandled IS NULL OR ce.IsHandled = @IsHandled
    ORDER BY ce.CreatedDateUtc DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_ContactEnquiry_MarkHandled
    @ContactEnquiryId  BIGINT,
    @HandledBy         BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sup.ContactEnquiry SET IsHandled = 1, HandledBy = @HandledBy, HandledDateUtc = SYSUTCDATETIME()
    WHERE ContactEnquiryId = @ContactEnquiryId;
    IF @@ROWCOUNT = 0
        THROW 50404, N'ENQUIRY_NOT_FOUND|Enquiry was not found.', 1;
END
GO

-- ===== Notifications =====

CREATE OR ALTER PROCEDURE sup.usp_NotificationTemplate_GetByCode
    @TemplateCode VARCHAR(60)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT nt.NotificationTemplateId, nt.TemplateCode, nt.NotificationChannelId, nt.Subject, nt.Body, nt.IsActive
    FROM sup.NotificationTemplate AS nt
    WHERE nt.TemplateCode = @TemplateCode AND nt.IsActive = 1;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_NotificationTemplate_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT nt.NotificationTemplateId, nt.TemplateCode, nt.NotificationChannelId, nt.Subject, nt.Body, nt.IsActive
    FROM sup.NotificationTemplate AS nt
    ORDER BY nt.TemplateCode, nt.NotificationChannelId;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_NotificationTemplate_Update
    @NotificationTemplateId  INT,
    @Subject                 NVARCHAR(200),
    @Body                    NVARCHAR(4000),
    @IsActive                BIT,
    @ModifiedBy              BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sup.NotificationTemplate
    SET Subject = @Subject, Body = @Body, IsActive = @IsActive, ModifiedBy = @ModifiedBy, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE NotificationTemplateId = @NotificationTemplateId;
    IF @@ROWCOUNT = 0
        THROW 50404, N'TEMPLATE_NOT_FOUND|Notification template was not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Notification_Create
    @UserId                 BIGINT,
    @NotificationChannelId  INT,
    @TemplateCode           VARCHAR(60),
    @Title                  NVARCHAR(200),
    @Message                NVARCHAR(2000),
    @NotificationStatusId   INT,
    @EntityType             VARCHAR(30),
    @EntityId               BIGINT,
    @FailureReason          NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO sup.Notification (UserId, NotificationChannelId, TemplateCode, Title, Message, NotificationStatusId, EntityType, EntityId, FailureReason, SentDateUtc)
    VALUES (@UserId, @NotificationChannelId, @TemplateCode, @Title, @Message, @NotificationStatusId, @EntityType, @EntityId, @FailureReason,
            CASE WHEN @NotificationStatusId = 2 THEN SYSUTCDATETIME() ELSE NULL END);
    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Notification_GetByUser
    @UserId      BIGINT,
    @PageNumber  INT,
    @PageSize    INT,
    @UnreadOnly  BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT n.NotificationId, n.TemplateCode, n.Title, n.Message, n.EntityType, n.EntityId, n.ReadDateUtc, n.CreatedDateUtc,
           COUNT(*) OVER () AS TotalRecords
    FROM sup.Notification AS n
    WHERE n.UserId = @UserId AND n.NotificationChannelId = 1 AND (@UnreadOnly = 0 OR n.ReadDateUtc IS NULL)
    ORDER BY n.CreatedDateUtc DESC, n.NotificationId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Notification_GetUnreadCount
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(COUNT(*) AS BIGINT) AS Id
    FROM sup.Notification
    WHERE UserId = @UserId AND NotificationChannelId = 1 AND ReadDateUtc IS NULL;
END
GO

CREATE OR ALTER PROCEDURE sup.usp_Notification_MarkRead
    @UserId          BIGINT,
    @NotificationId  BIGINT = NULL     -- NULL => mark all as read
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE sup.Notification SET ReadDateUtc = SYSUTCDATETIME()
    WHERE UserId = @UserId AND ReadDateUtc IS NULL AND NotificationChannelId = 1
      AND (@NotificationId IS NULL OR NotificationId = @NotificationId);
END
GO

-- ===== Audit =====

CREATE OR ALTER PROCEDURE aud.usp_AuditLog_Create
    @UserId      BIGINT,
    @Action      VARCHAR(100),
    @EntityType  VARCHAR(50),
    @EntityId    VARCHAR(50),
    @OldValue    NVARCHAR(4000),
    @NewValue    NVARCHAR(4000),
    @IpAddress   VARCHAR(45),
    @UserAgent   NVARCHAR(300),
    @TraceId     VARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO aud.AuditLog (UserId, Action, EntityType, EntityId, OldValue, NewValue, IpAddress, UserAgent, TraceId)
    VALUES (@UserId, @Action, @EntityType, @EntityId, @OldValue, @NewValue, @IpAddress, LEFT(@UserAgent, 300), @TraceId);
END
GO

CREATE OR ALTER PROCEDURE aud.usp_AuditLog_GetPaged
    @PageNumber   INT,
    @PageSize     INT,
    @UserId       BIGINT        = NULL,
    @EntityType   VARCHAR(50)   = NULL,
    @EntityId     VARCHAR(50)   = NULL,
    @Action       VARCHAR(100)  = NULL,
    @FromDateUtc  DATETIME2(3)  = NULL,
    @ToDateUtc    DATETIME2(3)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.AuditLogId, a.UserId, u.FullName AS UserName, a.Action, a.EntityType, a.EntityId, a.OldValue, a.NewValue,
           a.IpAddress, a.TraceId, a.CreatedDateUtc, COUNT(*) OVER () AS TotalRecords
    FROM aud.AuditLog AS a
    LEFT JOIN sec.[User] AS u ON u.UserId = a.UserId
    WHERE (@UserId IS NULL OR a.UserId = @UserId)
      AND (@EntityType IS NULL OR a.EntityType = @EntityType)
      AND (@EntityId IS NULL OR a.EntityId = @EntityId)
      AND (@Action IS NULL OR a.Action LIKE @Action + '%')
      AND (@FromDateUtc IS NULL OR a.CreatedDateUtc >= @FromDateUtc)
      AND (@ToDateUtc IS NULL OR a.CreatedDateUtc < @ToDateUtc)
    ORDER BY a.CreatedDateUtc DESC, a.AuditLogId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO
