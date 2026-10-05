using System.Text.Json.Serialization;
using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

// ---------------- Invoices ----------------

public sealed class InvoiceListItemDto : PagedRow
{
    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime InvoiceDateUtc { get; init; }
    public DateTime DueDateUtc { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public int InvoiceStatusId { get; init; }
    public bool IsOverdue { get; init; }
}

public sealed class InvoiceDto
{
    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerCompanyName { get; init; }
    public string? CustomerGstNumber { get; init; }
    [JsonIgnore] public string CustomerEmail { get; init; } = string.Empty;
    [JsonIgnore] public long CustomerUserId { get; init; }
    public long QuotationId { get; init; }
    public DateTime InvoiceDateUtc { get; init; }
    public DateTime DueDateUtc { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal BalanceAmount { get; init; }
    public int InvoiceStatusId { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class InvoiceItemDto
{
    public long InvoiceItemId { get; init; }
    public string ChargeCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal Amount { get; init; }
    public bool IsAdjustment { get; init; }
    public int SortOrder { get; init; }
}

public sealed record InvoiceDetailsResponse(
    InvoiceDto Invoice,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<PaymentListItemDto> Payments,
    IReadOnlyList<string> AvailableActions);

// ---------------- Payments ----------------

public sealed class PaymentListItemDto : PagedRow
{
    public long PaymentId { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal RefundedAmount { get; init; }
    public int PaymentMethodId { get; init; }
    public int PaymentStatusId { get; init; }
    public string GatewayName { get; init; } = string.Empty;
    public DateTime? PaymentDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class PaymentDto
{
    public long PaymentId { get; init; }
    public string PaymentNumber { get; init; } = string.Empty;
    public long InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    [JsonIgnore] public long CustomerUserId { get; init; }
    [JsonIgnore] public string CustomerEmail { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal RefundedAmount { get; init; }
    public int PaymentMethodId { get; init; }
    public int PaymentStatusId { get; init; }
    public string GatewayName { get; init; } = string.Empty;
    public string? GatewayOrderId { get; init; }
    public string? GatewayTransactionId { get; init; }
    public string? ReferenceNumber { get; init; }
    public DateTime? PaymentDateUtc { get; init; }
    public string? FailureReason { get; init; }
    public string? Remarks { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class PaymentRefundDto
{
    public long PaymentRefundId { get; init; }
    public string RefundNumber { get; init; } = string.Empty;
    public long PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int RefundStatusId { get; init; }
    public string? GatewayRefundId { get; init; }
    public DateTime? ProcessedDateUtc { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed record PaymentDetailsResponse(PaymentDto Payment, IReadOnlyList<PaymentRefundDto> Refunds);

/// <summary>Returned when an online payment is started: the browser opens the gateway checkout with these values.</summary>
public sealed record PaymentInitiatedResponse(long PaymentId, string PaymentNumber, bool IsExisting, GatewayCheckoutDto Checkout);

public sealed record GatewayCheckoutDto(string Gateway, string OrderId, string PublicKey, decimal Amount, string Currency, string? CheckoutUrl);

public sealed class ReconciliationRowDto
{
    public int PaymentMethodId { get; init; }
    public string PaymentMethodName { get; init; } = string.Empty;
    public string GatewayName { get; init; } = string.Empty;
    public int PaymentCount { get; init; }
    public decimal CollectedAmount { get; init; }
    public decimal RefundedAmount { get; init; }
    public decimal NetAmount { get; init; }
    public int FailedCount { get; init; }
    public int PendingCount { get; init; }
}

// ---------------- Settlements ----------------

public sealed class SettlementListItemDto : PagedRow
{
    public long SettlementId { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public decimal GrossAmount { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal NetAmount { get; init; }
    public int SettlementStatusId { get; init; }
    public DateTime? SettlementDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class SettlementDto
{
    public long SettlementId { get; init; }
    public string SettlementNumber { get; init; } = string.Empty;
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public string BookingNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public long? OwnerBankAccountId { get; init; }
    public string? BankName { get; init; }
    public string? AccountNumberLast4 { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal CommissionPercent { get; init; }
    public decimal CommissionAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal AdjustmentAmount { get; init; }
    public decimal NetAmount { get; init; }
    public int SettlementStatusId { get; init; }
    [JsonIgnore] public long? CreatedBy { get; init; }
    [JsonIgnore] public long OwnerUserId { get; init; }
    public DateTime? ApprovedDateUtc { get; init; }
    public DateTime? SettlementDateUtc { get; init; }
    public string? TransactionReference { get; init; }
    public string? FailureReason { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class SettlementItemDto
{
    public long SettlementItemId { get; init; }
    public string ItemType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public int SortOrder { get; init; }
}

public sealed record SettlementDetailsResponse(SettlementDto Settlement, IReadOnlyList<SettlementItemDto> Items, IReadOnlyList<string> AvailableActions);

public sealed class OwnerEarningsSummaryDto
{
    public decimal TotalEarned { get; init; }
    public decimal PendingAmount { get; init; }
    public int CompletedCount { get; init; }
    public int PendingCount { get; init; }
    public DateTime? LastSettlementDateUtc { get; init; }
}

public sealed class SettlementEligibleTripDto : PagedRow
{
    public long TripId { get; init; }
    public string TripNumber { get; init; } = string.Empty;
    public string BookingNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public decimal InvoiceSubTotal { get; init; }
    public DateTime? ActualDeliveryDateUtc { get; init; }
}
