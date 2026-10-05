using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProCargo.API.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;

namespace ProCargo.API.Controllers;

[Route("api/v1/support/tickets")]
[Authorize]
public sealed class SupportTicketsController : ApiControllerBase
{
    private readonly ISupportService _support;

    public SupportTicketsController(ISupportService support)
    {
        _support = support;
    }

    [HttpGet]
    public Task<PagedResult<SupportTicketListItemDto>> Get([FromQuery] SupportTicketSearchRequest request, CancellationToken cancellationToken) =>
        _support.GetTicketsAsync(request, cancellationToken);

    [HttpGet("{ticketId:long}")]
    public Task<SupportTicketDetailsResponse> GetById(long ticketId, CancellationToken cancellationToken) => _support.GetTicketAsync(ticketId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CreatedResponse>> Create(CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        var created = await _support.CreateTicketAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { ticketId = created.Id }, created);
    }

    [HttpPut("{ticketId:long}")]
    [HasPermission(Permissions.ManageSupport)]
    public async Task<IActionResult> Update(long ticketId, UpdateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        await _support.UpdateTicketAsync(ticketId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ticketId:long}/comments")]
    public async Task<CreatedResponse> AddComment(long ticketId, AddTicketCommentRequest request, CancellationToken cancellationToken) =>
        new(await _support.AddCommentAsync(ticketId, request, cancellationToken), null);
}

[Route("api/v1/support/complaints")]
[Authorize]
public sealed class ComplaintsController : ApiControllerBase
{
    private readonly ISupportService _support;

    public ComplaintsController(ISupportService support)
    {
        _support = support;
    }

    [HttpGet]
    public Task<PagedResult<ComplaintListItemDto>> Get([FromQuery] ComplaintSearchRequest request, CancellationToken cancellationToken) =>
        _support.GetComplaintsAsync(request, cancellationToken);

    [HttpGet("{complaintId:long}")]
    public Task<ComplaintDetailsResponse> GetById(long complaintId, CancellationToken cancellationToken) =>
        _support.GetComplaintAsync(complaintId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CreatedResponse>> Create(CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        var created = await _support.CreateComplaintAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { complaintId = created.Id }, created);
    }

    [HttpPost("{complaintId:long}/assign")]
    [HasPermission(Permissions.ManageComplaints)]
    public async Task<IActionResult> Assign(long complaintId, AssignComplaintRequest request, CancellationToken cancellationToken)
    {
        await _support.AssignComplaintAsync(complaintId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{complaintId:long}/status")]
    [HasPermission(Permissions.ManageComplaints)]
    public async Task<IActionResult> ChangeStatus(long complaintId, ChangeComplaintStatusRequest request, CancellationToken cancellationToken)
    {
        await _support.ChangeComplaintStatusAsync(complaintId, request, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/support/enquiries")]
[HasPermission(Permissions.ManageSupport)]
public sealed class ContactEnquiriesController : ApiControllerBase
{
    private readonly ISupportService _support;

    public ContactEnquiriesController(ISupportService support)
    {
        _support = support;
    }

    [HttpGet]
    public Task<PagedResult<ContactEnquiryDto>> Get([FromQuery] ContactEnquirySearchRequest request, CancellationToken cancellationToken) =>
        _support.GetContactEnquiriesAsync(request, cancellationToken);

    [HttpPost("{enquiryId:long}/handled")]
    public async Task<IActionResult> MarkHandled(long enquiryId, CancellationToken cancellationToken)
    {
        await _support.MarkContactEnquiryHandledAsync(enquiryId, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly IUserNotificationService _notifications;

    public NotificationsController(IUserNotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public Task<PagedResult<NotificationDto>> GetMine([FromQuery] NotificationSearchRequest request, CancellationToken cancellationToken) =>
        _notifications.GetMineAsync(request, cancellationToken);

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCount(CancellationToken cancellationToken) =>
        Ok(new { count = await _notifications.GetUnreadCountAsync(cancellationToken) });

    [HttpPost("{notificationId:long}/read")]
    public async Task<IActionResult> MarkRead(long notificationId, CancellationToken cancellationToken)
    {
        await _notifications.MarkReadAsync(notificationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await _notifications.MarkReadAsync(null, cancellationToken);
        return NoContent();
    }

    [HttpGet("templates")]
    [HasPermission(Permissions.ManageNotificationTemplates)]
    public Task<IReadOnlyList<NotificationTemplateDto>> GetTemplates(CancellationToken cancellationToken) =>
        _notifications.GetTemplatesAsync(cancellationToken);

    [HttpPut("templates/{templateId:int}")]
    [HasPermission(Permissions.ManageNotificationTemplates)]
    public async Task<IActionResult> UpdateTemplate(int templateId, UpdateNotificationTemplateRequest request, CancellationToken cancellationToken)
    {
        await _notifications.UpdateTemplateAsync(templateId, request, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/dashboard")]
[Authorize]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("operations")]
    [Authorize(Policy = Policies.Staff)]
    public Task<OperationsDashboardDto> GetOperations(CancellationToken cancellationToken) => _dashboard.GetOperationsAsync(cancellationToken);

    [HttpGet("customer")]
    [Authorize(Policy = Policies.External)]
    public Task<CustomerDashboardDto> GetCustomer(CancellationToken cancellationToken) => _dashboard.GetCustomerAsync(cancellationToken);

    [HttpGet("owner")]
    [Authorize(Policy = Policies.External)]
    public Task<OwnerDashboardDto> GetOwner(CancellationToken cancellationToken) => _dashboard.GetOwnerAsync(cancellationToken);

    [HttpGet("driver")]
    [Authorize(Policy = Policies.External)]
    public Task<DriverDashboardDto> GetDriver(CancellationToken cancellationToken) => _dashboard.GetDriverAsync(cancellationToken);
}

/// <summary>Management reports. Dates are IST calendar dates (inclusive); add /export to download CSV.</summary>
[Route("api/v1/reports")]
[HasPermission(Permissions.ViewReports)]
public sealed class ReportsController : ApiControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet("bookings-by-day")]
    public Task<IReadOnlyList<BookingsByDayDto>> BookingsByDay([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetBookingsByDayAsync(request, cancellationToken);

    [HttpGet("bookings-by-status")]
    public Task<IReadOnlyList<StatusCountDto>> BookingsByStatus([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetBookingsByStatusAsync(request, cancellationToken);

    [HttpGet("revenue")]
    public Task<IReadOnlyList<RevenueReportRowDto>> Revenue([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetRevenueAsync(request, cancellationToken);

    [HttpGet("trip-performance")]
    public Task<IReadOnlyList<TripPerformanceRowDto>> TripPerformance([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetTripPerformanceAsync(request, cancellationToken);

    [HttpGet("settlements")]
    public Task<IReadOnlyList<SettlementReportRowDto>> Settlements([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetSettlementsAsync(request, cancellationToken);

    [HttpGet("top-customers")]
    public Task<IReadOnlyList<TopCustomerDto>> TopCustomers([FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        _reports.GetTopCustomersAsync(request, cancellationToken);

    [HttpGet("partner-summary")]
    public Task<IReadOnlyList<PartnerSummaryRowDto>> PartnerSummary(CancellationToken cancellationToken) =>
        _reports.GetPartnerSummaryAsync(cancellationToken);

    [HttpGet("{report}/export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export(string report, [FromQuery] ReportPeriodRequest request, CancellationToken cancellationToken) =>
        ToFileResult(await _reports.ExportCsvAsync(report, request, cancellationToken));
}

[Route("api/v1/audit-logs")]
[HasPermission(Permissions.ViewAuditLogs)]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditQueryService _audit;

    public AuditLogsController(IAuditQueryService audit)
    {
        _audit = audit;
    }

    [HttpGet]
    public Task<PagedResult<AuditLogDto>> Get([FromQuery] AuditLogSearchRequest request, CancellationToken cancellationToken) =>
        _audit.GetPagedAsync(request, cancellationToken);
}
