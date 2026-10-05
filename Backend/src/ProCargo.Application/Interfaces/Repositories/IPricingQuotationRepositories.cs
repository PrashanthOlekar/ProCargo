using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Pricing;

namespace ProCargo.Application.Interfaces.Repositories;

public interface IPricingRepository
{
    Task<VehiclePricingRow?> GetVehiclePricingAsync(int vehicleTypeId, DateTime atUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistanceSlabRow>> GetDistanceSlabsAsync(int vehicleTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdditionalChargeRow>> GetAdditionalChargesAsync(int vehicleTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PricingRuleRow>> GetApplicableRulesAsync(int vehicleTypeId, int pickupCityId, int deliveryCityId, DateTime atUtc, CancellationToken cancellationToken);
    Task<TaxRateRow?> GetTaxRateAsync(string code, DateTime atUtc, CancellationToken cancellationToken);
    Task<CommissionRuleRow?> GetCommissionRuleAsync(int vehicleTypeId, DateTime atUtc, CancellationToken cancellationToken);

    Task<PricingConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<int> SaveVehiclePricingAsync(int? id, SaveVehiclePricingRequest request, long userId, CancellationToken cancellationToken);
    Task<int> SaveDistancePricingAsync(int? id, SaveDistancePricingRequest request, long userId, CancellationToken cancellationToken);
    Task<int> SaveAdditionalChargeAsync(int? id, SaveAdditionalChargeRequest request, long userId, CancellationToken cancellationToken);
    Task<int> SavePricingRuleAsync(int? id, SavePricingRuleRequest request, long userId, CancellationToken cancellationToken);
    Task<int> SaveTaxRateAsync(int? id, SaveTaxRateRequest request, long userId, CancellationToken cancellationToken);
    Task<int> SaveCommissionRuleAsync(int? id, SaveCommissionRuleRequest request, long userId, CancellationToken cancellationToken);
}

public sealed record SaveQuotationCommand(
    long? QuotationId,
    long BookingId,
    PriceBreakdown Price,
    DateTime ValidityDateUtc,
    string? Notes,
    byte[]? RowVersion,
    long UserId);

public interface IQuotationRepository
{
    Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationSearchRequest request, long? customerScope, bool excludeDrafts, CancellationToken cancellationToken);
    Task<QuotationDto?> GetByIdAsync(long quotationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<QuotationChargeDto>> GetChargesAsync(long quotationId, CancellationToken cancellationToken);
    Task<CreatedResult> CreateAsync(SaveQuotationCommand command, CancellationToken cancellationToken);
    Task UpdateAsync(SaveQuotationCommand command, CancellationToken cancellationToken);
    Task SendAsync(long quotationId, long modifiedBy, CancellationToken cancellationToken);
    Task AcceptAsync(long quotationId, long customerId, long modifiedBy, CancellationToken cancellationToken);
    Task RejectAsync(long quotationId, long customerId, string reason, long modifiedBy, CancellationToken cancellationToken);
    Task WithdrawAsync(long quotationId, string reason, long modifiedBy, CancellationToken cancellationToken);
    Task<int> ExpireOverdueAsync(DateTime asOfUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<StatusHistoryDto>> GetStatusHistoryAsync(long quotationId, CancellationToken cancellationToken);
}
