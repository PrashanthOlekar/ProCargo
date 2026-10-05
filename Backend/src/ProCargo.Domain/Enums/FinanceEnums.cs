namespace ProCargo.Domain.Enums;

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Pending = 1,
    Initiated = 2,
    Authorized = 3,
    Paid = 4,
    Failed = 5,
    Refunded = 6,
    PartiallyRefunded = 7,
    PartiallyPaid = 8,
    Cancelled = 9
}

public enum RefundStatus
{
    Pending = 1,
    Processed = 2,
    Failed = 3
}

public enum PaymentMethod
{
    Upi = 1,
    Card = 2,
    NetBanking = 3,
    Wallet = 4,
    BankTransfer = 5,
    Cash = 6,
    Cheque = 7
}

public enum SettlementStatus
{
    Pending = 1,
    Approved = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}
