using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly StoredProcedureExecutor _sp;

    public InvoiceRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceSearchRequest request, long? customerScope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("InvoiceDate", "InvoiceDate", "DueDate", "TotalAmount", "InvoiceNumber");
        var rows = await _sp.QueryAsync<InvoiceListItemDto>($"""
            EXEC fin.usp_Invoice_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @CustomerId={customerScope},
                @InvoiceStatusId={(int?)request.Status}, @OverdueOnly={request.OverdueOnly}, @FromDateUtc={request.FromDateUtc},
                @ToDateUtc={request.ToDateUtc}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<InvoiceDto?> GetByIdAsync(long invoiceId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<InvoiceDto>($"EXEC fin.usp_Invoice_GetById @InvoiceId={invoiceId}", cancellationToken);

    public Task<IReadOnlyList<InvoiceItemDto>> GetItemsAsync(long invoiceId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<InvoiceItemDto>($"EXEC fin.usp_InvoiceItem_GetByInvoice @InvoiceId={invoiceId}", cancellationToken);

    public Task<CreatedResult> CreateAsync(long bookingId, int paymentTermsDays, long createdBy, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>(
            $"EXEC fin.usp_Invoice_Create @BookingId={bookingId}, @PaymentTermsDays={paymentTermsDays}, @CreatedBy={createdBy}",
            cancellationToken);

    public Task AddAdjustmentAsync(long invoiceId, string description, decimal amount, byte[] rowVersion, long createdBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC fin.usp_Invoice_AddAdjustment @InvoiceId={invoiceId}, @Description={description}, @Amount={amount},
                @CreatedBy={createdBy}, @RowVersion={rowVersion}
            """, cancellationToken);

    public Task CancelAsync(long invoiceId, string reason, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC fin.usp_Invoice_Cancel @InvoiceId={invoiceId}, @Reason={reason}, @ModifiedBy={modifiedBy}", cancellationToken);
}

internal sealed class PaymentRepository : IPaymentRepository
{
    private readonly StoredProcedureExecutor _sp;

    public PaymentRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<PaymentListItemDto>> GetPagedAsync(PaymentSearchRequest request, long? customerScope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "PaymentDate", "Amount", "PaymentNumber");
        var rows = await _sp.QueryAsync<PaymentListItemDto>($"""
            EXEC fin.usp_Payment_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @CustomerId={customerScope},
                @InvoiceId={request.InvoiceId}, @PaymentStatusId={(int?)request.Status}, @PaymentMethodId={(int?)request.Method},
                @FromDateUtc={request.FromDateUtc}, @ToDateUtc={request.ToDateUtc}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<PaymentDto?> GetByIdAsync(long paymentId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<PaymentDto>($"EXEC fin.usp_Payment_GetById @PaymentId={paymentId}", cancellationToken);

    public Task<PaymentDto?> GetByGatewayOrderAsync(string gatewayName, string gatewayOrderId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<PaymentDto>(
            $"EXEC fin.usp_Payment_GetById @PaymentId={(long?)null}, @GatewayName={gatewayName}, @GatewayOrderId={gatewayOrderId}",
            cancellationToken);

    public Task<IdempotentResult> CreateAsync(CreatePaymentCommand c, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<IdempotentResult>($"""
            EXEC fin.usp_Payment_Create @InvoiceId={c.InvoiceId}, @Amount={c.Amount}, @PaymentMethodId={(int)c.Method},
                @PaymentStatusId={(int)c.Status}, @GatewayName={c.GatewayName}, @IdempotencyKey={c.IdempotencyKey},
                @ReferenceNumber={c.ReferenceNumber}, @Remarks={c.Remarks}, @CreatedBy={c.CreatedBy}
            """, cancellationToken);

    public Task SetGatewayOrderAsync(long paymentId, string gatewayOrderId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC fin.usp_Payment_SetGatewayOrder @PaymentId={paymentId}, @GatewayOrderId={gatewayOrderId}", cancellationToken);

    public Task<IdWithFlagResult> UpdateStatusAsync(PaymentStatusUpdate u, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<IdWithFlagResult>($"""
            EXEC fin.usp_Payment_UpdateStatus @PaymentId={u.PaymentId}, @NewStatusId={(int)u.NewStatus},
                @GatewayTransactionId={u.GatewayTransactionId}, @GatewayEventId={u.GatewayEventId}, @ResponseCode={u.ResponseCode},
                @ResponseMessage={u.ResponseMessage}, @ModifiedBy={u.ModifiedBy}
            """, cancellationToken);

    public Task<CreatedResult> CreateRefundAsync(long paymentId, decimal amount, string reason, long createdBy, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC fin.usp_PaymentRefund_Create @PaymentId={paymentId}, @Amount={amount}, @Reason={reason}, @CreatedBy={createdBy}
            """, cancellationToken);

    public Task CompleteRefundAsync(long refundId, bool isSuccess, string? gatewayRefundId, string? failureReason, long modifiedBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC fin.usp_PaymentRefund_Complete @PaymentRefundId={refundId}, @IsSuccess={isSuccess}, @GatewayRefundId={gatewayRefundId},
                @FailureReason={failureReason}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task<IReadOnlyList<PaymentRefundDto>> GetRefundsAsync(long paymentId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<PaymentRefundDto>($"EXEC fin.usp_PaymentRefund_GetByPayment @PaymentId={paymentId}", cancellationToken);

    public Task<IReadOnlyList<ReconciliationRowDto>> GetReconciliationAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<ReconciliationRowDto>($"EXEC fin.usp_Payment_GetReconciliation @FromUtc={fromUtc}, @ToUtc={toUtc}", cancellationToken);
}

internal sealed class SettlementRepository : ISettlementRepository
{
    private readonly StoredProcedureExecutor _sp;

    public SettlementRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<SettlementListItemDto>> GetPagedAsync(SettlementSearchRequest request, long? ownerScope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "SettlementDate", "NetAmount", "SettlementNumber");
        var rows = await _sp.QueryAsync<SettlementListItemDto>($"""
            EXEC fin.usp_Settlement_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @OwnerId={ownerScope},
                @SettlementStatusId={(int?)request.Status}, @FromDateUtc={request.FromDateUtc}, @ToDateUtc={request.ToDateUtc},
                @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<SettlementDto?> GetByIdAsync(long settlementId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<SettlementDto>($"EXEC fin.usp_Settlement_GetById @SettlementId={settlementId}", cancellationToken);

    public Task<IReadOnlyList<SettlementItemDto>> GetItemsAsync(long settlementId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<SettlementItemDto>($"EXEC fin.usp_SettlementItem_GetBySettlement @SettlementId={settlementId}", cancellationToken);

    public Task<CreatedResult> CreateAsync(long tripId, decimal commissionPercent, decimal minimumCommission, decimal tdsPercent, long createdBy,
        CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC fin.usp_Settlement_Create @TripId={tripId}, @CommissionPercent={commissionPercent},
                @MinimumCommission={minimumCommission}, @TdsPercent={tdsPercent}, @CreatedBy={createdBy}
            """, cancellationToken);

    public Task AddAdjustmentAsync(long settlementId, string description, decimal amount, long createdBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC fin.usp_Settlement_AddAdjustment @SettlementId={settlementId}, @Description={description}, @Amount={amount}, @CreatedBy={createdBy}
            """, cancellationToken);

    public Task ChangeStatusAsync(long settlementId, SettlementStatus expected, SettlementStatus next, string? remarks, long changedBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC fin.usp_Settlement_ChangeStatus @SettlementId={settlementId}, @ExpectedStatusId={(int)expected}, @NewStatusId={(int)next},
                @Remarks={remarks}, @ChangedBy={changedBy}
            """, cancellationToken);

    public Task CompleteAsync(long settlementId, string transactionReference, long changedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC fin.usp_Settlement_Complete @SettlementId={settlementId}, @TransactionReference={transactionReference}, @ChangedBy={changedBy}
            """, cancellationToken);

    public async Task<OwnerEarningsSummaryDto> GetOwnerSummaryAsync(long ownerId, CancellationToken cancellationToken) =>
        await _sp.QuerySingleOrDefaultAsync<OwnerEarningsSummaryDto>($"EXEC fin.usp_Settlement_GetOwnerSummary @OwnerId={ownerId}", cancellationToken)
        ?? new OwnerEarningsSummaryDto();

    public async Task<PagedResult<SettlementEligibleTripDto>> GetEligibleTripsAsync(PagedRequest request, CancellationToken cancellationToken)
    {
        var (page, size, _, _, _) = request.Normalize("DeliveryDate");
        var rows = await _sp.QueryAsync<SettlementEligibleTripDto>(
            $"EXEC fin.usp_Settlement_GetEligibleTrips @PageNumber={page}, @PageSize={size}", cancellationToken);
        return rows.ToPagedResult(page, size);
    }
}
