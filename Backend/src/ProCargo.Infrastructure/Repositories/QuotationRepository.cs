using System.Text.Json;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Pricing;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

/// <summary>Quotation persistence. Charge lines travel as JSON so header + lines are written atomically.</summary>
internal sealed class QuotationRepository : IQuotationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly StoredProcedureExecutor _sp;

    public QuotationRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationSearchRequest request, long? customerScope, bool excludeDrafts,
        CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "QuotationNumber", "TotalAmount", "ValidityDate");
        var rows = await _sp.QueryAsync<QuotationListItemDto>($"""
            EXEC core.usp_Quotation_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @CustomerId={customerScope},
                @BookingId={request.BookingId}, @QuotationStatusId={(int?)request.Status}, @ExcludeDrafts={excludeDrafts},
                @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<QuotationDto?> GetByIdAsync(long quotationId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<QuotationDto>($"EXEC core.usp_Quotation_GetById @QuotationId={quotationId}", cancellationToken);

    public Task<IReadOnlyList<QuotationChargeDto>> GetChargesAsync(long quotationId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<QuotationChargeDto>($"EXEC core.usp_QuotationCharge_GetByQuotation @QuotationId={quotationId}", cancellationToken);

    public Task<CreatedResult> CreateAsync(SaveQuotationCommand c, CancellationToken cancellationToken)
    {
        var p = c.Price;
        return _sp.QuerySingleAsync<CreatedResult>($"""
            EXEC core.usp_Quotation_Create @BookingId={c.BookingId}, @DistanceKm={p.DistanceKm}, @BaseAmount={p.BaseAmount},
                @DistanceCharge={p.DistanceCharge}, @LoadingCharge={p.LoadingCharge}, @UnloadingCharge={p.UnloadingCharge},
                @WaitingCharge={p.WaitingCharge}, @TollCharge={p.TollCharge}, @NightCharge={p.NightCharge},
                @SpecialHandlingCharge={p.SpecialHandlingCharge}, @AdjustmentAmount={p.AdjustmentAmount}, @DiscountAmount={p.DiscountAmount},
                @SubTotal={p.SubTotal}, @TaxPercent={p.TaxPercent}, @TaxAmount={p.TaxAmount}, @TotalAmount={p.TotalAmount},
                @ValidityDateUtc={c.ValidityDateUtc}, @Notes={c.Notes}, @ChargesJson={SqlParams.Json("ChargesJson", ChargesJson(p))},
                @CreatedBy={c.UserId}
            """, cancellationToken);
    }

    public Task UpdateAsync(SaveQuotationCommand c, CancellationToken cancellationToken)
    {
        var p = c.Price;
        return _sp.ExecuteAsync($"""
            EXEC core.usp_Quotation_Update @QuotationId={c.QuotationId}, @DistanceKm={p.DistanceKm}, @BaseAmount={p.BaseAmount},
                @DistanceCharge={p.DistanceCharge}, @LoadingCharge={p.LoadingCharge}, @UnloadingCharge={p.UnloadingCharge},
                @WaitingCharge={p.WaitingCharge}, @TollCharge={p.TollCharge}, @NightCharge={p.NightCharge},
                @SpecialHandlingCharge={p.SpecialHandlingCharge}, @AdjustmentAmount={p.AdjustmentAmount}, @DiscountAmount={p.DiscountAmount},
                @SubTotal={p.SubTotal}, @TaxPercent={p.TaxPercent}, @TaxAmount={p.TaxAmount}, @TotalAmount={p.TotalAmount},
                @ValidityDateUtc={c.ValidityDateUtc}, @Notes={c.Notes}, @ChargesJson={SqlParams.Json("ChargesJson", ChargesJson(p))},
                @ModifiedBy={c.UserId}, @RowVersion={c.RowVersion}
            """, cancellationToken);
    }

    public Task SendAsync(long quotationId, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Quotation_Send @QuotationId={quotationId}, @ModifiedBy={modifiedBy}", cancellationToken);

    public Task AcceptAsync(long quotationId, long customerId, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Quotation_Accept @QuotationId={quotationId}, @CustomerId={customerId}, @ModifiedBy={modifiedBy}",
            cancellationToken);

    public Task RejectAsync(long quotationId, long customerId, string reason, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Quotation_Reject @QuotationId={quotationId}, @CustomerId={customerId}, @Reason={reason}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task WithdrawAsync(long quotationId, string reason, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Quotation_Withdraw @QuotationId={quotationId}, @Reason={reason}, @ModifiedBy={modifiedBy}",
            cancellationToken);

    public Task<int> ExpireOverdueAsync(DateTime asOfUtc, CancellationToken cancellationToken) =>
        _sp.QueryScalarAsync<int>($"EXEC core.usp_Quotation_ExpireOverdue @AsOfUtc={asOfUtc}", cancellationToken);

    public Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long quotationId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<StatusHistoryDto>($"EXEC core.usp_Quotation_GetStatusHistory @QuotationId={quotationId}", cancellationToken);

    private static string ChargesJson(PriceBreakdown price) =>
        JsonSerializer.Serialize(price.Lines.Select(l => new
        {
            chargeCode = l.ChargeCode,
            description = l.Description,
            quantity = l.Quantity,
            unitRate = l.UnitRate,
            amount = l.Amount,
            sortOrder = l.SortOrder
        }), JsonOptions);
}
