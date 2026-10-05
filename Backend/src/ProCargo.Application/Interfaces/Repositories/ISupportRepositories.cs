using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

public interface ISupportRepository
{
    Task<CreatedResult> CreateTicketAsync(long raisedBy, CreateSupportTicketRequest request, CancellationToken cancellationToken);
    Task<SupportTicketDto?> GetTicketAsync(long ticketId, CancellationToken cancellationToken);
    Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketSearchRequest request, long? raisedBy, long? assignedTo,
        CancellationToken cancellationToken);
    Task UpdateTicketAsync(long ticketId, UpdateSupportTicketRequest request, long modifiedBy, CancellationToken cancellationToken);
    Task<long> AddCommentAsync(long ticketId, string text, bool isInternal, long createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicketCommentDto>> GetCommentsAsync(long ticketId, bool includeInternal, CancellationToken cancellationToken);

    Task<CreatedResult> CreateComplaintAsync(long raisedBy, CreateComplaintRequest request, CancellationToken cancellationToken);
    Task<ComplaintDto?> GetComplaintAsync(long complaintId, CancellationToken cancellationToken);
    Task<PagedResult<ComplaintListItemDto>> GetComplaintsAsync(ComplaintSearchRequest request, long? raisedBy, long? assignedTo,
        CancellationToken cancellationToken);
    Task AssignComplaintAsync(long complaintId, long assignedTo, long modifiedBy, CancellationToken cancellationToken);
    Task ChangeComplaintStatusAsync(long complaintId, ComplaintStatus expected, ComplaintStatus next, string? resolution, long modifiedBy,
        CancellationToken cancellationToken);

    Task<long> CreateContactEnquiryAsync(ContactEnquiryRequest request, string? normalizedPhone, string? ipAddress, CancellationToken cancellationToken);
    Task<PagedResult<ContactEnquiryDto>> GetContactEnquiriesAsync(ContactEnquirySearchRequest request, CancellationToken cancellationToken);
    Task MarkContactEnquiryHandledAsync(long enquiryId, long handledBy, CancellationToken cancellationToken);
}

public sealed record CreateNotificationCommand(
    long UserId,
    NotificationChannel Channel,
    string TemplateCode,
    string Title,
    string Message,
    NotificationStatus Status,
    string? EntityType,
    long? EntityId,
    string? FailureReason);

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationTemplateDto>> GetTemplatesByCodeAsync(string templateCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationTemplateDto>> GetAllTemplatesAsync(CancellationToken cancellationToken);
    Task UpdateTemplateAsync(int templateId, UpdateNotificationTemplateRequest request, long modifiedBy, CancellationToken cancellationToken);
    Task<long> CreateAsync(CreateNotificationCommand command, CancellationToken cancellationToken);
    Task<PagedResult<NotificationDto>> GetByUserAsync(long userId, NotificationSearchRequest request, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(long userId, CancellationToken cancellationToken);
    Task MarkReadAsync(long userId, long? notificationId, CancellationToken cancellationToken);
}

public sealed record AuditEntry(
    long? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string? UserAgent,
    string? TraceId);

public interface IAuditRepository
{
    Task CreateAsync(AuditEntry entry, CancellationToken cancellationToken);
    Task<PagedResult<AuditLogDto>> GetPagedAsync(AuditLogSearchRequest request, CancellationToken cancellationToken);
}

public interface IReportRepository
{
    Task<OperationsDashboardDto> GetOperationsDashboardAsync(DateTime todayStartUtc, DateTime monthStartUtc, CancellationToken cancellationToken);
    Task<CustomerDashboardDto> GetCustomerDashboardAsync(long customerId, CancellationToken cancellationToken);
    Task<OwnerDashboardDto> GetOwnerDashboardAsync(long ownerId, CancellationToken cancellationToken);
    Task<DriverDashboardDto> GetDriverDashboardAsync(long driverId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookingsByDayDto>> GetBookingsByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusCountDto>> GetBookingsByStatusAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueAsync(DateTime fromUtc, DateTime toUtc, string groupBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripPerformanceRowDto>> GetTripPerformanceAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementReportRowDto>> GetSettlementsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopCustomerDto>> GetTopCustomersAsync(DateTime fromUtc, DateTime toUtc, int top, CancellationToken cancellationToken);
    Task<IReadOnlyList<PartnerSummaryRowDto>> GetPartnerSummaryAsync(CancellationToken cancellationToken);
}
