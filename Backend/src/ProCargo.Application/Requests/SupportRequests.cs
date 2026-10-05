using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

public sealed class CreateSupportTicketRequest
{
    public long? BookingId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public sealed class UpdateSupportTicketRequest
{
    public long? AssignedToUserId { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
}

public sealed class AddTicketCommentRequest
{
    public string CommentText { get; set; } = string.Empty;

    /// <summary>Internal notes are visible to staff only (ignored for external users).</summary>
    public bool IsInternal { get; set; }
}

public sealed class SupportTicketSearchRequest : PagedRequest
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public bool AssignedToMe { get; set; }
}

public sealed class CreateComplaintRequest
{
    public long? BookingId { get; set; }
    public long? TripId { get; set; }
    public string Category { get; set; } = "Other";
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ComplaintSearchRequest : PagedRequest
{
    public ComplaintStatus? Status { get; set; }
    public string? Category { get; set; }
    public bool AssignedToMe { get; set; }
}

public sealed class AssignComplaintRequest
{
    public long AssignedToUserId { get; set; }
}

public sealed class ChangeComplaintStatusRequest
{
    public ComplaintStatus Status { get; set; }
    public string? Resolution { get; set; }
}

public sealed class ContactEnquiryRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Honeypot: real users never fill this hidden field.</summary>
    public string? Website { get; set; }
}

public sealed class ContactEnquirySearchRequest : PagedRequest
{
    public bool? IsHandled { get; set; }
}

public sealed class UpdateNotificationTemplateRequest
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class NotificationSearchRequest : PagedRequest
{
    public bool UnreadOnly { get; set; }
}

public sealed class AuditLogSearchRequest : PagedRequest
{
    public long? UserId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }
}

/// <summary>Report period as IST calendar dates (inclusive). Converted to UTC boundaries in the service.</summary>
public sealed class ReportPeriodRequest
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Revenue only: Day or Month.</summary>
    public string GroupBy { get; set; } = "Day";

    /// <summary>Top customers only.</summary>
    public int Top { get; set; } = 10;
}
