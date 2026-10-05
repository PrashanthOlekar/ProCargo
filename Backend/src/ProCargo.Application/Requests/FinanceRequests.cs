using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

public sealed class InvoiceSearchRequest : PagedRequest
{
    public InvoiceStatus? Status { get; set; }
    public bool OverdueOnly { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }

    /// <summary>Staff only.</summary>
    public long? CustomerId { get; set; }
}

public sealed class CreateInvoiceRequest
{
    public long BookingId { get; set; }

    /// <summary>Defaults to the Invoice.PaymentTermsDays setting.</summary>
    public int? PaymentTermsDays { get; set; }
}

public sealed class InvoiceAdjustmentRequest
{
    public string Description { get; set; } = string.Empty;

    /// <summary>Positive for an extra charge, negative for a credit.</summary>
    public decimal Amount { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed class CancelInvoiceRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class PaymentSearchRequest : PagedRequest
{
    public long? InvoiceId { get; set; }
    public PaymentStatus? Status { get; set; }
    public PaymentMethod? Method { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }

    /// <summary>Staff only.</summary>
    public long? CustomerId { get; set; }
}

/// <summary>Customer starts an online payment. Idempotency-Key header makes retries safe.</summary>
public sealed class InitiatePaymentRequest
{
    public long InvoiceId { get; set; }

    /// <summary>Defaults to the outstanding balance.</summary>
    public decimal? Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Upi;
}

/// <summary>Values the gateway checkout returns to the browser after payment.</summary>
public sealed class ConfirmPaymentRequest
{
    public string GatewayOrderId { get; set; } = string.Empty;
    public string GatewayPaymentId { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

/// <summary>Finance records a payment received outside the gateway (cash, cheque, bank transfer).</summary>
public sealed class RecordOfflinePaymentRequest
{
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public sealed class RefundPaymentRequest
{
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReconciliationRequest
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}

public sealed class SettlementSearchRequest : PagedRequest
{
    public SettlementStatus? Status { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }

    /// <summary>Staff only.</summary>
    public long? OwnerId { get; set; }
}

public sealed class CreateSettlementRequest
{
    public long TripId { get; set; }
}

public sealed class SettlementAdjustmentRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class CompleteSettlementRequest
{
    public string TransactionReference { get; set; } = string.Empty;
}

public sealed class SettlementReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}
