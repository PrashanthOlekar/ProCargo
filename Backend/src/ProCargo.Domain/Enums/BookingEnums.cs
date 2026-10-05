namespace ProCargo.Domain.Enums;

/// <summary>Booking lifecycle. Ids mirror mst.BookingStatus.</summary>
public enum BookingStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Quoted = 4,
    Confirmed = 5,
    Assigned = 6,
    InTransit = 7,
    Delivered = 8,
    Invoiced = 9,
    Paid = 10,
    Closed = 11,
    Cancelled = 12,
    OnHold = 13,
    Rejected = 14
}

/// <summary>Quotation lifecycle. Ids mirror mst.QuotationStatus.</summary>
public enum QuotationStatus
{
    Draft = 1,
    Sent = 2,
    Accepted = 3,
    Rejected = 4,
    Expired = 5,
    Withdrawn = 6
}
