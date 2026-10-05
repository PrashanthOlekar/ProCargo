using Microsoft.Extensions.Logging;
using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.DomainRules;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Support;

/// <summary>
/// Support tickets, complaints and public contact enquiries.
///   * Any signed-in external user (RaiseSupportRequests) raises tickets/complaints and sees only their own.
///   * Staff with ManageSupport / ManageComplaints see and work all of them; internal notes stay internal.
///   * A booking or trip referenced by an external user must be one they can access (404 otherwise).
/// </summary>
public sealed class SupportService : ISupportService
{
    private readonly ISupportRepository _support;
    private readonly IBookingService _bookings;
    private readonly ITripService _trips;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly ILogger<SupportService> _logger;

    public SupportService(ISupportRepository support, IBookingService bookings, ITripService trips, INotificationService notifications,
        AccessGuard access, IAuditLogger audit, ILogger<SupportService> logger)
    {
        _support = support;
        _bookings = bookings;
        _trips = trips;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _logger = logger;
    }

    // ---------------- tickets

    public async Task<CreatedResponse> CreateTicketAsync(CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        EnsureCanRaise(Permissions.ManageSupport);
        if (request.BookingId is long bookingId) await _bookings.GetAccessibleAsync(bookingId, cancellationToken);

        var created = await _support.CreateTicketAsync(_access.User.UserId, request, cancellationToken);
        await _audit.LogAsync("TicketCreated", "SupportTicket", created.Id, newValue: new { created.Number, request.Subject },
            cancellationToken: cancellationToken);
        return new CreatedResponse(created.Id, created.Number);
    }

    public Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketSearchRequest request, CancellationToken cancellationToken)
    {
        if (_access.IsStaffWith(Permissions.ManageSupport))
        {
            return _support.GetTicketsAsync(request, null, request.AssignedToMe ? _access.User.UserId : null, cancellationToken);
        }

        return _support.GetTicketsAsync(request, _access.User.UserId, null, cancellationToken);
    }

    public async Task<SupportTicketDetailsResponse> GetTicketAsync(long ticketId, CancellationToken cancellationToken)
    {
        var ticket = await GetAccessibleTicketAsync(ticketId, cancellationToken);
        var comments = await _support.GetCommentsAsync(ticketId, _access.IsStaffWith(Permissions.ManageSupport), cancellationToken);
        return new SupportTicketDetailsResponse(ticket, comments);
    }

    public async Task UpdateTicketAsync(long ticketId, UpdateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSupport);
        var ticket = await GetAccessibleTicketAsync(ticketId, cancellationToken);

        var current = (TicketStatus)ticket.TicketStatusId;
        if (current != request.Status) StatusRules.Ticket.EnsureCanTransition(current, request.Status);

        await _support.UpdateTicketAsync(ticketId, request, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("TicketUpdated", "SupportTicket", ticketId,
            oldValue: new { Status = current.ToString(), ticket.TicketPriorityId, ticket.AssignedToUserId },
            newValue: new { Status = request.Status.ToString(), Priority = request.Priority.ToString(), request.AssignedToUserId },
            cancellationToken: cancellationToken);

        if (current != request.Status)
        {
            await _notifications.NotifyUserAsync(ticket.RaisedByUserId, NotificationTemplates.TicketUpdated, new Dictionary<string, string>
            {
                ["TicketNumber"] = ticket.TicketNumber,
                ["Status"] = request.Status.ToString()
            }, new NotificationSubject("SupportTicket", ticketId), cancellationToken);
        }
    }

    public async Task<long> AddCommentAsync(long ticketId, AddTicketCommentRequest request, CancellationToken cancellationToken)
    {
        var ticket = await GetAccessibleTicketAsync(ticketId, cancellationToken);
        var isStaff = _access.IsStaffWith(Permissions.ManageSupport);

        if (ticket.TicketStatusId == (int)TicketStatus.Closed)
        {
            throw new BusinessRuleException("TICKET_CLOSED", "Closed tickets cannot receive comments. Raise a new ticket.");
        }

        var id = await _support.AddCommentAsync(ticketId, request.CommentText.Trim(), isStaff && request.IsInternal, _access.User.UserId,
            cancellationToken);

        if (isStaff && !request.IsInternal && ticket.RaisedByUserId != _access.User.UserId)
        {
            await _notifications.NotifyUserAsync(ticket.RaisedByUserId, NotificationTemplates.TicketUpdated, new Dictionary<string, string>
            {
                ["TicketNumber"] = ticket.TicketNumber,
                ["Status"] = "New reply"
            }, new NotificationSubject("SupportTicket", ticketId), cancellationToken);
        }

        return id;
    }

    // ---------------- complaints

    public async Task<CreatedResponse> CreateComplaintAsync(CreateComplaintRequest request, CancellationToken cancellationToken)
    {
        EnsureCanRaise(Permissions.ManageComplaints);
        if (request.BookingId is long bookingId) await _bookings.GetAccessibleAsync(bookingId, cancellationToken);
        if (request.TripId is long tripId) await _trips.GetByIdAsync(tripId, cancellationToken);

        var created = await _support.CreateComplaintAsync(_access.User.UserId, request, cancellationToken);
        await _audit.LogAsync("ComplaintCreated", "Complaint", created.Id,
            newValue: new { created.Number, request.Category, request.Subject }, cancellationToken: cancellationToken);
        return new CreatedResponse(created.Id, created.Number);
    }

    public Task<PagedResult<ComplaintListItemDto>> GetComplaintsAsync(ComplaintSearchRequest request, CancellationToken cancellationToken)
    {
        if (_access.IsStaffWith(Permissions.ManageComplaints))
        {
            return _support.GetComplaintsAsync(request, null, request.AssignedToMe ? _access.User.UserId : null, cancellationToken);
        }

        return _support.GetComplaintsAsync(request, _access.User.UserId, null, cancellationToken);
    }

    public async Task<ComplaintDetailsResponse> GetComplaintAsync(long complaintId, CancellationToken cancellationToken)
    {
        var complaint = await GetAccessibleComplaintAsync(complaintId, cancellationToken);
        var actions = new List<string>();
        if (_access.IsStaffWith(Permissions.ManageComplaints))
        {
            actions.Add("Assign");
            actions.AddRange(StatusRules.Complaint.AllowedFrom((ComplaintStatus)complaint.ComplaintStatusId).Select(s => s.ToString()));
        }

        return new ComplaintDetailsResponse(complaint, actions);
    }

    public async Task AssignComplaintAsync(long complaintId, AssignComplaintRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageComplaints);
        var complaint = await GetAccessibleComplaintAsync(complaintId, cancellationToken);

        await _support.AssignComplaintAsync(complaintId, request.AssignedToUserId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("ComplaintAssigned", "Complaint", complaintId,
            oldValue: new { complaint.AssignedToUserId }, newValue: new { request.AssignedToUserId }, cancellationToken: cancellationToken);
    }

    public async Task ChangeComplaintStatusAsync(long complaintId, ChangeComplaintStatusRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageComplaints);
        var complaint = await GetAccessibleComplaintAsync(complaintId, cancellationToken);
        var current = (ComplaintStatus)complaint.ComplaintStatusId;
        StatusRules.Complaint.EnsureCanTransition(current, request.Status);

        await _support.ChangeComplaintStatusAsync(complaintId, current, request.Status, request.Resolution?.Trim(), _access.User.UserId,
            cancellationToken);
        await _audit.LogAsync("ComplaintStatusChanged", "Complaint", complaintId,
            oldValue: new { Status = current.ToString() }, newValue: new { Status = request.Status.ToString(), request.Resolution },
            cancellationToken: cancellationToken);

        await _notifications.NotifyUserAsync(complaint.RaisedByUserId, NotificationTemplates.ComplaintUpdated, new Dictionary<string, string>
        {
            ["ComplaintNumber"] = complaint.ComplaintNumber,
            ["Status"] = request.Status.ToString()
        }, new NotificationSubject("Complaint", complaintId), cancellationToken);
    }

    // ---------------- contact enquiries

    public async Task SubmitContactEnquiryAsync(ContactEnquiryRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Website))
        {
            // Honeypot filled in: almost certainly a bot. Pretend success, store nothing.
            _logger.LogInformation("Contact enquiry discarded by honeypot");
            return;
        }

        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : IndianFormats.NormalizePhone(request.PhoneNumber);
        await _support.CreateContactEnquiryAsync(request, phone, _access.User.IpAddress, cancellationToken);
    }

    public Task<PagedResult<ContactEnquiryDto>> GetContactEnquiriesAsync(ContactEnquirySearchRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSupport);
        return _support.GetContactEnquiriesAsync(request, cancellationToken);
    }

    public async Task MarkContactEnquiryHandledAsync(long enquiryId, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageSupport);
        await _support.MarkContactEnquiryHandledAsync(enquiryId, _access.User.UserId, cancellationToken);
        await _audit.LogAsync("ContactEnquiryHandled", "ContactEnquiry", enquiryId, cancellationToken: cancellationToken);
    }

    // ---------------- helpers

    private void EnsureCanRaise(string staffPermission)
    {
        if (_access.User.HasPermission(Permissions.RaiseSupportRequests) || _access.IsStaffWith(staffPermission)) return;
        throw new ForbiddenException();
    }

    private async Task<SupportTicketDto> GetAccessibleTicketAsync(long ticketId, CancellationToken cancellationToken)
    {
        var ticket = await _support.GetTicketAsync(ticketId, cancellationToken) ?? throw NotFoundException.For("SupportTicket", ticketId);
        if (_access.IsStaffWith(Permissions.ManageSupport) || ticket.RaisedByUserId == _access.User.UserId) return ticket;
        throw NotFoundException.For("SupportTicket", ticketId);
    }

    private async Task<ComplaintDto> GetAccessibleComplaintAsync(long complaintId, CancellationToken cancellationToken)
    {
        var complaint = await _support.GetComplaintAsync(complaintId, cancellationToken) ?? throw NotFoundException.For("Complaint", complaintId);
        if (_access.IsStaffWith(Permissions.ManageComplaints) || complaint.RaisedByUserId == _access.User.UserId) return complaint;
        throw NotFoundException.For("Complaint", complaintId);
    }
}
