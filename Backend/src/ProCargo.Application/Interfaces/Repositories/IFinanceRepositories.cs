using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

public interface IInvoiceRepository
{
    Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceSearchRequest request, long? customerScope, CancellationToken cancellationToken);
    Task<InvoiceDto?> GetByIdAsync(long invoiceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvoiceItemDto>> GetItemsAsync(long invoiceId, CancellationToken cancellationToken);
    Task<CreatedResult> CreateAsync(long bookingId, int paymentTermsDays, long createdBy, CancellationToken cancellationToken);
    Task AddAdjustmentAsync(long invoiceId, string description, decimal amount, byte[] rowVersion, long createdBy, CancellationToken cancellationToken);
    Task CancelAsync(long invoiceId, string reason, long modifiedBy, CancellationToken cancellationToken);
}

public sealed record CreatePaymentCommand(
    long InvoiceId,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    string GatewayName,
    string? IdempotencyKey,
    string? ReferenceNumber,
    string? Remarks,
    long? CreatedBy);

public sealed record PaymentStatusUpdate(
    long PaymentId,
    PaymentStatus NewStatus,
    string? GatewayTransactionId,
    string? GatewayEventId,
    string? ResponseCode,
    string? ResponseMessage,
    long? ModifiedBy);

public interface IPaymentRepository
{
    Task<PagedResult<PaymentListItemDto>> GetPagedAsync(PaymentSearchRequest request, long? customerScope, CancellationToken cancellationToken);
    Task<PaymentDto?> GetByIdAsync(long paymentId, CancellationToken cancellationToken);
    Task<PaymentDto?> GetByGatewayOrderAsync(string gatewayName, string gatewayOrderId, CancellationToken cancellationToken);
    Task<IdempotentResult> CreateAsync(CreatePaymentCommand command, CancellationToken cancellationToken);
    Task SetGatewayOrderAsync(long paymentId, string gatewayOrderId, CancellationToken cancellationToken);
    Task<IdWithFlagResult> UpdateStatusAsync(PaymentStatusUpdate update, CancellationToken cancellationToken);
    Task<CreatedResult> CreateRefundAsync(long paymentId, decimal amount, string reason, long createdBy, CancellationToken cancellationToken);
    Task CompleteRefundAsync(long refundId, bool isSuccess, string? gatewayRefundId, string? failureReason, long modifiedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentRefundDto>> GetRefundsAsync(long paymentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationRowDto>> GetReconciliationAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public interface ISettlementRepository
{
    Task<PagedResult<SettlementListItemDto>> GetPagedAsync(SettlementSearchRequest request, long? ownerScope, CancellationToken cancellationToken);
    Task<SettlementDto?> GetByIdAsync(long settlementId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SettlementItemDto>> GetItemsAsync(long settlementId, CancellationToken cancellationToken);
    Task<CreatedResult> CreateAsync(long tripId, decimal commissionPercent, decimal minimumCommission, decimal tdsPercent, long createdBy,
        CancellationToken cancellationToken);
    Task AddAdjustmentAsync(long settlementId, string description, decimal amount, long createdBy, CancellationToken cancellationToken);
    Task ChangeStatusAsync(long settlementId, SettlementStatus expected, SettlementStatus next, string? remarks, long changedBy,
        CancellationToken cancellationToken);
    Task CompleteAsync(long settlementId, string transactionReference, long changedBy, CancellationToken cancellationToken);
    Task<OwnerEarningsSummaryDto> GetOwnerSummaryAsync(long ownerId, CancellationToken cancellationToken);
    Task<PagedResult<SettlementEligibleTripDto>> GetEligibleTripsAsync(PagedRequest request, CancellationToken cancellationToken);
}
