using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Pricing;

namespace ProCargo.Application.Interfaces.Services;

public interface IPricingService
{
    /// <summary>Indicative price for the public website / customer portal.</summary>
    Task<PriceEstimateResponse> EstimateAsync(PriceEstimateRequest request, CancellationToken cancellationToken);

    /// <summary>Full price of a booking for a quotation.</summary>
    Task<PriceBreakdown> PriceBookingAsync(BookingDetailsDto booking, QuotationPricingRequest inputs, CancellationToken cancellationToken);

    Task<PricingConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<int> SaveVehiclePricingAsync(int? id, SaveVehiclePricingRequest request, CancellationToken cancellationToken);
    Task<int> SaveDistancePricingAsync(int? id, SaveDistancePricingRequest request, CancellationToken cancellationToken);
    Task<int> SaveAdditionalChargeAsync(int? id, SaveAdditionalChargeRequest request, CancellationToken cancellationToken);
    Task<int> SavePricingRuleAsync(int? id, SavePricingRuleRequest request, CancellationToken cancellationToken);
    Task<int> SaveTaxRateAsync(int? id, SaveTaxRateRequest request, CancellationToken cancellationToken);
    Task<int> SaveCommissionRuleAsync(int? id, SaveCommissionRuleRequest request, CancellationToken cancellationToken);
}

public interface IQuotationService
{
    Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationSearchRequest request, CancellationToken cancellationToken);
    Task<QuotationDetailsResponse> GetByIdAsync(long quotationId, CancellationToken cancellationToken);
    Task<PriceEstimateResponse> PreviewAsync(PreviewQuotationRequest request, CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateQuotationRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long quotationId, UpdateQuotationRequest request, CancellationToken cancellationToken);
    Task SendAsync(long quotationId, CancellationToken cancellationToken);
    Task AcceptAsync(long quotationId, CancellationToken cancellationToken);
    Task RejectAsync(long quotationId, RejectQuotationRequest request, CancellationToken cancellationToken);
    Task WithdrawAsync(long quotationId, WithdrawQuotationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(long quotationId, CancellationToken cancellationToken);
    Task<int> ExpireOverdueAsync(CancellationToken cancellationToken);
}
