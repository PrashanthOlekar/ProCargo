using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Interfaces.Services;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceSearchRequest request, CancellationToken cancellationToken);
    Task<InvoiceDetailsResponse> GetByIdAsync(long invoiceId, CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken);
    Task AddAdjustmentAsync(long invoiceId, InvoiceAdjustmentRequest request, CancellationToken cancellationToken);
    Task CancelAsync(long invoiceId, CancelInvoiceRequest request, CancellationToken cancellationToken);
}

public interface IPaymentService
{
    Task<PagedResult<PaymentListItemDto>> GetPagedAsync(PaymentSearchRequest request, CancellationToken cancellationToken);
    Task<PaymentDetailsResponse> GetByIdAsync(long paymentId, CancellationToken cancellationToken);
    Task<PaymentInitiatedResponse> InitiateAsync(InitiatePaymentRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentDto> ConfirmAsync(long paymentId, ConfirmPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Development only (Sandbox gateway): completes the checkout as the gateway would.</summary>
    Task<PaymentDto> SimulateSandboxCheckoutAsync(long paymentId, CancellationToken cancellationToken);

    /// <summary>Gateway webhook (anonymous endpoint, authenticated by the HMAC signature). Idempotent per event id.</summary>
    Task HandleWebhookAsync(string rawBody, string? signature, CancellationToken cancellationToken);

    Task<CreatedResponse> RecordOfflineAsync(RecordOfflinePaymentRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<CreatedResponse> RefundAsync(long paymentId, RefundPaymentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationRowDto>> GetReconciliationAsync(ReconciliationRequest request, CancellationToken cancellationToken);
}

public interface ISettlementService
{
    Task<PagedResult<SettlementListItemDto>> GetPagedAsync(SettlementSearchRequest request, CancellationToken cancellationToken);
    Task<SettlementDetailsResponse> GetByIdAsync(long settlementId, CancellationToken cancellationToken);
    Task<PagedResult<SettlementEligibleTripDto>> GetEligibleTripsAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateSettlementRequest request, CancellationToken cancellationToken);
    Task AddAdjustmentAsync(long settlementId, SettlementAdjustmentRequest request, CancellationToken cancellationToken);
    Task ApproveAsync(long settlementId, CancellationToken cancellationToken);
    Task StartProcessingAsync(long settlementId, CancellationToken cancellationToken);
    Task CompleteAsync(long settlementId, CompleteSettlementRequest request, CancellationToken cancellationToken);
    Task FailAsync(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken);
    Task CancelAsync(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken);
    Task<OwnerEarningsSummaryDto> GetOwnerSummaryAsync(long? ownerId, CancellationToken cancellationToken);
}
