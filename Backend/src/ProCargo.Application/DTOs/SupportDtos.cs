using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

// ---------------- Support tickets ----------------

public sealed class SupportTicketListItemDto : PagedRow
{
    public long SupportTicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string RaisedByName { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public int TicketPriorityId { get; init; }
    public int TicketStatusId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class SupportTicketDto
{
    public long SupportTicketId { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public long RaisedByUserId { get; init; }
    public string RaisedByName { get; init; } = string.Empty;
    public long? BookingId { get; init; }
    public string? BookingNumber { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TicketPriorityId { get; init; }
    public int TicketStatusId { get; init; }
    public long? AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime? ClosedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class SupportTicketCommentDto
{
    public long SupportTicketCommentId { get; init; }
    public string CommentText { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public long CreatedBy { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public bool IsStaff { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed record SupportTicketDetailsResponse(SupportTicketDto Ticket, IReadOnlyList<SupportTicketCommentDto> Comments);

// ---------------- Complaints ----------------

public sealed class ComplaintListItemDto : PagedRow
{
    public long ComplaintId { get; init; }
    public string ComplaintNumber { get; init; } = string.Empty;
    public string RaisedByName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public int ComplaintStatusId { get; init; }
    public string? AssignedToName { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class ComplaintDto
{
    public long ComplaintId { get; init; }
    public string ComplaintNumber { get; init; } = string.Empty;
    public long RaisedByUserId { get; init; }
    public string RaisedByName { get; init; } = string.Empty;
    public long? BookingId { get; init; }
    public string? BookingNumber { get; init; }
    public long? TripId { get; init; }
    public string? TripNumber { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int ComplaintStatusId { get; init; }
    public long? AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public string? Resolution { get; init; }
    public DateTime? ResolvedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed record ComplaintDetailsResponse(ComplaintDto Complaint, IReadOnlyList<string> AvailableActions);

// ---------------- Contact enquiries (public website) ----------------

public sealed class ContactEnquiryDto : PagedRow
{
    public long ContactEnquiryId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsHandled { get; init; }
    public DateTime? HandledDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

// ---------------- Notifications ----------------

public sealed class NotificationTemplateDto
{
    public int NotificationTemplateId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public int NotificationChannelId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class NotificationDto : PagedRow
{
    public long NotificationId { get; init; }
    public string TemplateCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? EntityType { get; init; }
    public long? EntityId { get; init; }
    public DateTime? ReadDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

// ---------------- Audit ----------------

public sealed class AuditLogDto : PagedRow
{
    public long AuditLogId { get; init; }
    public long? UserId { get; init; }
    public string? UserName { get; init; }
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? IpAddress { get; init; }
    public string? TraceId { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
