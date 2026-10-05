namespace ProCargo.Domain.Enums;

public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    WaitingOnCustomer = 3,
    Resolved = 4,
    Closed = 5
}

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ComplaintStatus
{
    Open = 1,
    Assigned = 2,
    Investigating = 3,
    Resolved = 4,
    Rejected = 5,
    Closed = 6
}

public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Sms = 3,
    Push = 4
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}
