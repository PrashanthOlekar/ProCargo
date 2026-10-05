using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class SupportRepository : ISupportRepository
{
    private readonly StoredProcedureExecutor _sp;

    public SupportRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<CreatedResult> CreateTicketAsync(long raisedBy, CreateSupportTicketRequest r, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC sup.usp_SupportTicket_Create @RaisedByUserId={raisedBy}, @BookingId={r.BookingId}, @Subject={r.Subject.Trim()},
                @Description={r.Description.Trim()}, @TicketPriorityId={(int)r.Priority}
            """, cancellationToken);

    public Task<SupportTicketDto?> GetTicketAsync(long ticketId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<SupportTicketDto>($"EXEC sup.usp_SupportTicket_GetById @SupportTicketId={ticketId}", cancellationToken);

    public async Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketSearchRequest request, long? raisedBy, long? assignedTo,
        CancellationToken cancellationToken)
    {
        var (page, size, search, _, _) = request.Normalize("CreatedDate");
        var rows = await _sp.QueryAsync<SupportTicketListItemDto>($"""
            EXEC sup.usp_SupportTicket_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @RaisedByUserId={raisedBy},
                @AssignedToUserId={assignedTo}, @TicketStatusId={(int?)request.Status}, @TicketPriorityId={(int?)request.Priority}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task UpdateTicketAsync(long ticketId, UpdateSupportTicketRequest r, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sup.usp_SupportTicket_Update @SupportTicketId={ticketId}, @AssignedToUserId={r.AssignedToUserId},
                @TicketStatusId={(int)r.Status}, @TicketPriorityId={(int)r.Priority}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public async Task<long> AddCommentAsync(long ticketId, string text, bool isInternal, long createdBy, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC sup.usp_SupportTicketComment_Create @SupportTicketId={ticketId}, @CommentText={text}, @IsInternal={isInternal}, @CreatedBy={createdBy}
            """, cancellationToken);
        return result.Id;
    }

    public Task<IReadOnlyList<SupportTicketCommentDto>> GetCommentsAsync(long ticketId, bool includeInternal, CancellationToken cancellationToken) =>
        _sp.QueryAsync<SupportTicketCommentDto>(
            $"EXEC sup.usp_SupportTicketComment_GetByTicket @SupportTicketId={ticketId}, @IncludeInternal={includeInternal}", cancellationToken);

    public Task<CreatedResult> CreateComplaintAsync(long raisedBy, CreateComplaintRequest r, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC sup.usp_Complaint_Create @RaisedByUserId={raisedBy}, @BookingId={r.BookingId}, @TripId={r.TripId}, @Category={r.Category},
                @Subject={r.Subject.Trim()}, @Description={r.Description.Trim()}
            """, cancellationToken);

    public Task<ComplaintDto?> GetComplaintAsync(long complaintId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<ComplaintDto>($"EXEC sup.usp_Complaint_GetById @ComplaintId={complaintId}", cancellationToken);

    public async Task<PagedResult<ComplaintListItemDto>> GetComplaintsAsync(ComplaintSearchRequest request, long? raisedBy, long? assignedTo,
        CancellationToken cancellationToken)
    {
        var (page, size, search, _, _) = request.Normalize("CreatedDate");
        var rows = await _sp.QueryAsync<ComplaintListItemDto>($"""
            EXEC sup.usp_Complaint_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @RaisedByUserId={raisedBy},
                @AssignedToUserId={assignedTo}, @ComplaintStatusId={(int?)request.Status}, @Category={request.Category}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task AssignComplaintAsync(long complaintId, long assignedTo, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sup.usp_Complaint_Assign @ComplaintId={complaintId}, @AssignedToUserId={assignedTo}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task ChangeComplaintStatusAsync(long complaintId, ComplaintStatus expected, ComplaintStatus next, string? resolution, long modifiedBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sup.usp_Complaint_ChangeStatus @ComplaintId={complaintId}, @ExpectedStatusId={(int)expected}, @NewStatusId={(int)next},
                @Resolution={resolution}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public async Task<long> CreateContactEnquiryAsync(ContactEnquiryRequest r, string? normalizedPhone, string? ipAddress, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC sup.usp_ContactEnquiry_Create @FullName={r.FullName.Trim()}, @Email={r.Email.Trim().ToLowerInvariant()},
                @PhoneNumber={normalizedPhone}, @Subject={r.Subject.Trim()}, @Message={r.Message.Trim()}, @IpAddress={ipAddress}
            """, cancellationToken);
        return result.Id;
    }

    public async Task<PagedResult<ContactEnquiryDto>> GetContactEnquiriesAsync(ContactEnquirySearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, _, _, _) = request.Normalize("CreatedDate");
        var rows = await _sp.QueryAsync<ContactEnquiryDto>(
            $"EXEC sup.usp_ContactEnquiry_GetPaged @PageNumber={page}, @PageSize={size}, @IsHandled={request.IsHandled}", cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task MarkContactEnquiryHandledAsync(long enquiryId, long handledBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sup.usp_ContactEnquiry_MarkHandled @ContactEnquiryId={enquiryId}, @HandledBy={handledBy}", cancellationToken);
}

internal sealed class NotificationRepository : INotificationRepository
{
    private readonly StoredProcedureExecutor _sp;

    public NotificationRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<IReadOnlyList<NotificationTemplateDto>> GetTemplatesByCodeAsync(string templateCode, CancellationToken cancellationToken) =>
        _sp.QueryAsync<NotificationTemplateDto>($"EXEC sup.usp_NotificationTemplate_GetByCode @TemplateCode={templateCode}", cancellationToken);

    public Task<IReadOnlyList<NotificationTemplateDto>> GetAllTemplatesAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<NotificationTemplateDto>($"EXEC sup.usp_NotificationTemplate_GetAll", cancellationToken);

    public Task UpdateTemplateAsync(int templateId, UpdateNotificationTemplateRequest r, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC sup.usp_NotificationTemplate_Update @NotificationTemplateId={templateId}, @Subject={r.Subject}, @Body={r.Body},
                @IsActive={r.IsActive}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public async Task<long> CreateAsync(CreateNotificationCommand c, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC sup.usp_Notification_Create @UserId={c.UserId}, @NotificationChannelId={(int)c.Channel}, @TemplateCode={c.TemplateCode},
                @Title={c.Title}, @Message={c.Message}, @NotificationStatusId={(int)c.Status}, @EntityType={c.EntityType},
                @EntityId={c.EntityId}, @FailureReason={c.FailureReason}
            """, cancellationToken);
        return result.Id;
    }

    public async Task<PagedResult<NotificationDto>> GetByUserAsync(long userId, NotificationSearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, _, _, _) = request.Normalize("CreatedDate");
        var rows = await _sp.QueryAsync<NotificationDto>($"""
            EXEC sup.usp_Notification_GetByUser @UserId={userId}, @PageNumber={page}, @PageSize={size}, @UnreadOnly={request.UnreadOnly}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public async Task<int> GetUnreadCountAsync(long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"EXEC sup.usp_Notification_GetUnreadCount @UserId={userId}", cancellationToken);
        return (int)result.Id;
    }

    public Task MarkReadAsync(long userId, long? notificationId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC sup.usp_Notification_MarkRead @UserId={userId}, @NotificationId={notificationId}", cancellationToken);
}

internal sealed class AuditRepository : IAuditRepository
{
    private readonly StoredProcedureExecutor _sp;

    public AuditRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task CreateAsync(AuditEntry e, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC aud.usp_AuditLog_Create @UserId={e.UserId}, @Action={e.Action}, @EntityType={e.EntityType}, @EntityId={e.EntityId},
                @OldValue={e.OldValue}, @NewValue={e.NewValue}, @IpAddress={e.IpAddress}, @UserAgent={e.UserAgent}, @TraceId={e.TraceId}
            """, cancellationToken);

    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogSearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, _, _, _) = request.Normalize("CreatedDate");
        var rows = await _sp.QueryAsync<AuditLogDto>($"""
            EXEC aud.usp_AuditLog_GetPaged @PageNumber={page}, @PageSize={size}, @UserId={request.UserId}, @EntityType={request.EntityType},
                @EntityId={request.EntityId}, @Action={request.Action}, @FromDateUtc={request.FromDateUtc}, @ToDateUtc={request.ToDateUtc}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }
}

internal sealed class ReportRepository : IReportRepository
{
    private readonly StoredProcedureExecutor _sp;

    public ReportRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<OperationsDashboardDto> GetOperationsDashboardAsync(DateTime todayStartUtc, DateTime monthStartUtc, CancellationToken cancellationToken) =>
        await _sp.QuerySingleOrDefaultAsync<OperationsDashboardDto>(
            $"EXEC rpt.usp_Dashboard_GetOperationsKpis @TodayStartUtc={todayStartUtc}, @MonthStartUtc={monthStartUtc}", cancellationToken)
        ?? new OperationsDashboardDto();

    public async Task<CustomerDashboardDto> GetCustomerDashboardAsync(long customerId, CancellationToken cancellationToken) =>
        await _sp.QuerySingleOrDefaultAsync<CustomerDashboardDto>($"EXEC rpt.usp_Dashboard_GetCustomerSummary @CustomerId={customerId}", cancellationToken)
        ?? new CustomerDashboardDto();

    public async Task<OwnerDashboardDto> GetOwnerDashboardAsync(long ownerId, CancellationToken cancellationToken) =>
        await _sp.QuerySingleOrDefaultAsync<OwnerDashboardDto>($"EXEC rpt.usp_Dashboard_GetOwnerSummary @OwnerId={ownerId}", cancellationToken)
        ?? new OwnerDashboardDto();

    public async Task<DriverDashboardDto> GetDriverDashboardAsync(long driverId, CancellationToken cancellationToken) =>
        await _sp.QuerySingleOrDefaultAsync<DriverDashboardDto>($"EXEC rpt.usp_Dashboard_GetDriverSummary @DriverId={driverId}", cancellationToken)
        ?? new DriverDashboardDto();

    public Task<IReadOnlyList<BookingsByDayDto>> GetBookingsByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<BookingsByDayDto>($"EXEC rpt.usp_Report_BookingsByDay @FromUtc={fromUtc}, @ToUtc={toUtc}", cancellationToken);

    public Task<IReadOnlyList<StatusCountDto>> GetBookingsByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<StatusCountDto>($"EXEC rpt.usp_Report_BookingsByStatus @FromUtc={fromUtc}, @ToUtc={toUtc}", cancellationToken);

    public Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueAsync(DateTime fromUtc, DateTime toUtc, string groupBy, CancellationToken cancellationToken) =>
        _sp.QueryAsync<RevenueReportRowDto>($"EXEC rpt.usp_Report_Revenue @FromUtc={fromUtc}, @ToUtc={toUtc}, @GroupBy={groupBy}", cancellationToken);

    public Task<IReadOnlyList<TripPerformanceRowDto>> GetTripPerformanceAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<TripPerformanceRowDto>($"EXEC rpt.usp_Report_TripPerformance @FromUtc={fromUtc}, @ToUtc={toUtc}", cancellationToken);

    public Task<IReadOnlyList<SettlementReportRowDto>> GetSettlementsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<SettlementReportRowDto>($"EXEC rpt.usp_Report_Settlements @FromUtc={fromUtc}, @ToUtc={toUtc}", cancellationToken);

    public Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken cancellationToken) =>
        _sp.QueryAsync<TopCustomerDto>($"EXEC rpt.usp_Report_TopCustomers @FromUtc={fromUtc}, @ToUtc={toUtc}, @Top={top}", cancellationToken);

    public Task<IReadOnlyList<PartnerSummaryRowDto>> GetPartnerSummaryAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<PartnerSummaryRowDto>($"EXEC rpt.usp_Report_PartnerSummary", cancellationToken);
}
