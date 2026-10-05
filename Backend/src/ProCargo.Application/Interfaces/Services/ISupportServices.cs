using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Interfaces.Services;

public interface ISupportService
{
    Task<CreatedResponse> CreateTicketAsync(CreateSupportTicketRequest request, CancellationToken cancellationToken);
    Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketSearchRequest request, CancellationToken cancellationToken);
    Task<SupportTicketDetailsResponse> GetTicketAsync(long ticketId, CancellationToken cancellationToken);
    Task UpdateTicketAsync(long ticketId, UpdateSupportTicketRequest request, CancellationToken cancellationToken);
    Task<long> AddCommentAsync(long ticketId, AddTicketCommentRequest request, CancellationToken cancellationToken);

    Task<CreatedResponse> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ComplaintListItemDto>> GetComplaintsAsync(ComplaintSearchRequest request, CancellationToken cancellationToken);
    Task<ComplaintDetailsResponse> GetComplaintAsync(long complaintId, CancellationToken cancellationToken);
    Task AssignComplaintAsync(long complaintId, AssignComplaintRequest request, CancellationToken cancellationToken);
    Task ChangeComplaintStatusAsync(long complaintId, ChangeComplaintStatusRequest request, CancellationToken cancellationToken);

    Task SubmitContactEnquiryAsync(ContactEnquiryRequest request, CancellationToken cancellationToken);
    Task<PagedResult<ContactEnquiryDto>> GetContactEnquiriesAsync(ContactEnquirySearchRequest request, CancellationToken cancellationToken);
    Task MarkContactEnquiryHandledAsync(long enquiryId, CancellationToken cancellationToken);
}

/// <summary>The signed-in user's in-app notifications and template administration.</summary>
public interface IUserNotificationService
{
    Task<PagedResult<NotificationDto>> GetMineAsync(NotificationSearchRequest request, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);
    Task MarkReadAsync(long? notificationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task UpdateTemplateAsync(int templateId, UpdateNotificationTemplateRequest request, CancellationToken cancellationToken);
}

public interface IAuditQueryService
{
    Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogSearchRequest request, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<OperationsDashboardDto> GetOperationsAsync(CancellationToken cancellationToken);
    Task<CustomerDashboardDto> GetCustomerAsync(CancellationToken cancellationToken);
    Task<OwnerDashboardDto> GetOwnerAsync(CancellationToken cancellationToken);
    Task<DriverDashboardDto> GetDriverAsync(CancellationToken cancellationToken);
}

public interface IReportService
{
    Task<IReadOnlyList<BookingsByDayDto>> GetBookingsByDayAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusCountDto>> GetBookingsByStatusAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripPerformanceRowDto>> GetTripPerformanceAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementReportRowDto>> GetSettlementsAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(ReportPeriodRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PartnerSummaryRowDto>> GetPartnerSummaryAsync(CancellationToken cancellationToken);

    /// <summary>CSV export of a report (UTF-8 with BOM so Excel opens it correctly; formula injection neutralised).</summary>
    Task<FileDownload> ExportCsvAsync(string report, ReportPeriodRequest request, CancellationToken cancellationToken);
}
