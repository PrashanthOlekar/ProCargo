using ProCargo.Domain.Enums;

namespace ProCargo.Domain.DomainRules;

/// <summary>
/// The single source of truth for which status changes are legal.
/// Examples of rules enforced here:
///   * a completed/closed trip can never go back to Scheduled,
///   * a cancelled booking can never be assigned a vehicle or driver,
///   * a trip can only start after pickup was verified with the customer's OTP.
/// </summary>
public static class StatusRules
{
    public static readonly StatusTransitionMap<BookingStatus> Booking = new StatusTransitionMap<BookingStatus>("Booking")
        .Allow(BookingStatus.Draft, BookingStatus.Submitted, BookingStatus.Cancelled)
        .Allow(BookingStatus.Submitted, BookingStatus.UnderReview, BookingStatus.Rejected, BookingStatus.Cancelled, BookingStatus.OnHold)
        .Allow(BookingStatus.UnderReview, BookingStatus.Quoted, BookingStatus.Rejected, BookingStatus.Cancelled, BookingStatus.OnHold)
        .Allow(BookingStatus.Quoted, BookingStatus.Confirmed, BookingStatus.UnderReview, BookingStatus.Cancelled, BookingStatus.OnHold)
        .Allow(BookingStatus.Confirmed, BookingStatus.Assigned, BookingStatus.Cancelled, BookingStatus.OnHold)
        .Allow(BookingStatus.Assigned, BookingStatus.InTransit, BookingStatus.Confirmed, BookingStatus.Cancelled)
        .Allow(BookingStatus.InTransit, BookingStatus.Delivered)
        .Allow(BookingStatus.Delivered, BookingStatus.Invoiced)
        .Allow(BookingStatus.Invoiced, BookingStatus.Paid, BookingStatus.Delivered)
        .Allow(BookingStatus.Paid, BookingStatus.Closed)
        .Allow(BookingStatus.OnHold, BookingStatus.Submitted, BookingStatus.UnderReview, BookingStatus.Quoted, BookingStatus.Confirmed, BookingStatus.Cancelled);

    public static readonly StatusTransitionMap<TripStatus> Trip = new StatusTransitionMap<TripStatus>("Trip")
        .Allow(TripStatus.Scheduled, TripStatus.PickupVerified, TripStatus.Cancelled, TripStatus.OnHold)
        .Allow(TripStatus.PickupVerified, TripStatus.InTransit, TripStatus.OnHold, TripStatus.Exception)
        .Allow(TripStatus.InTransit, TripStatus.Delivered, TripStatus.OnHold, TripStatus.Exception)
        .Allow(TripStatus.Delivered, TripStatus.PodUploaded)
        .Allow(TripStatus.PodUploaded, TripStatus.Completed)
        .Allow(TripStatus.Completed, TripStatus.Closed, TripStatus.PodUploaded)
        .Allow(TripStatus.OnHold, TripStatus.Scheduled, TripStatus.PickupVerified, TripStatus.InTransit, TripStatus.Cancelled)
        .Allow(TripStatus.Exception, TripStatus.InTransit, TripStatus.PickupVerified, TripStatus.OnHold, TripStatus.Cancelled);

    public static readonly StatusTransitionMap<QuotationStatus> Quotation = new StatusTransitionMap<QuotationStatus>("Quotation")
        .Allow(QuotationStatus.Draft, QuotationStatus.Sent, QuotationStatus.Withdrawn)
        .Allow(QuotationStatus.Sent, QuotationStatus.Accepted, QuotationStatus.Rejected, QuotationStatus.Expired, QuotationStatus.Withdrawn);

    public static readonly StatusTransitionMap<SettlementStatus> Settlement = new StatusTransitionMap<SettlementStatus>("Settlement")
        .Allow(SettlementStatus.Pending, SettlementStatus.Approved, SettlementStatus.Cancelled)
        .Allow(SettlementStatus.Approved, SettlementStatus.Processing, SettlementStatus.Cancelled)
        .Allow(SettlementStatus.Processing, SettlementStatus.Completed, SettlementStatus.Failed);

    public static readonly StatusTransitionMap<ComplaintStatus> Complaint = new StatusTransitionMap<ComplaintStatus>("Complaint")
        .Allow(ComplaintStatus.Open, ComplaintStatus.Assigned, ComplaintStatus.Investigating, ComplaintStatus.Resolved, ComplaintStatus.Rejected)
        .Allow(ComplaintStatus.Assigned, ComplaintStatus.Investigating, ComplaintStatus.Resolved, ComplaintStatus.Rejected)
        .Allow(ComplaintStatus.Investigating, ComplaintStatus.Resolved, ComplaintStatus.Rejected)
        .Allow(ComplaintStatus.Resolved, ComplaintStatus.Closed, ComplaintStatus.Investigating);

    public static readonly StatusTransitionMap<TicketStatus> Ticket = new StatusTransitionMap<TicketStatus>("Ticket")
        .Allow(TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingOnCustomer, TicketStatus.Resolved, TicketStatus.Closed)
        .Allow(TicketStatus.InProgress, TicketStatus.WaitingOnCustomer, TicketStatus.Resolved, TicketStatus.Closed)
        .Allow(TicketStatus.WaitingOnCustomer, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed)
        .Allow(TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.InProgress);

    /// <summary>Booking statuses in which the customer may still edit the booking.</summary>
    public static bool IsBookingEditable(BookingStatus status) =>
        status is BookingStatus.Draft or BookingStatus.Submitted;

    /// <summary>Booking statuses the customer may cancel from (before the goods are picked up).</summary>
    public static bool IsBookingCancellableByCustomer(BookingStatus status) =>
        status is BookingStatus.Draft or BookingStatus.Submitted or BookingStatus.UnderReview
            or BookingStatus.Quoted or BookingStatus.Confirmed;

    /// <summary>Trip statuses that keep a vehicle and driver occupied.</summary>
    public static bool IsTripActive(TripStatus status) =>
        status is TripStatus.Scheduled or TripStatus.PickupVerified or TripStatus.InTransit
            or TripStatus.OnHold or TripStatus.Exception;

    /// <summary>Trip statuses during which tracking points are accepted.</summary>
    public static bool IsTripTrackable(TripStatus status) =>
        status is TripStatus.PickupVerified or TripStatus.InTransit or TripStatus.Exception or TripStatus.OnHold;
}
