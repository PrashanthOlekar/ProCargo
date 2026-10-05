using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Entities;
using ProCargo.Domain.Pricing;

namespace ProCargo.Application.Services.Pricing;

/// <summary>
/// Loads the effective pricing configuration from the fin.* tables and runs the pure
/// <see cref="PricingCalculator"/>. No price, rate or percentage is hard-coded anywhere.
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly IPricingRepository _pricing;
    private readonly IMasterDataRepository _masterData;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;

    public PricingService(IPricingRepository pricing, IMasterDataRepository masterData, AccessGuard access, IAuditLogger audit, TimeProvider clock)
    {
        _pricing = pricing;
        _masterData = masterData;
        _access = access;
        _audit = audit;
        _clock = clock;
    }

    public async Task<PriceEstimateResponse> EstimateAsync(PriceEstimateRequest request, CancellationToken cancellationToken)
    {
        var vehicleType = (await _masterData.GetVehicleTypesAsync(false, cancellationToken)).FirstOrDefault(v => v.VehicleTypeId == request.VehicleTypeId)
                          ?? throw new BusinessRuleException("VEHICLE_TYPE_INVALID", "The selected vehicle type is not available.");

        if (request.WeightKg > vehicleType.CapacityKg)
        {
            throw new BusinessRuleException("WEIGHT_EXCEEDS_CAPACITY", $"{vehicleType.Name} carries up to {vehicleType.CapacityKg:0} kg.");
        }

        var config = await LoadConfigurationAsync(request.VehicleTypeId, request.PickupCityId, request.DeliveryCityId, cancellationToken);
        var price = PricingCalculator.Calculate(new PricingRequestContext(
            request.DistanceKm, request.WeightKg, request.IncludeLoading, request.IncludeUnloading, 0, 0,
            request.IsNightMovement, request.RequiresSpecialHandling, 0, 0, null), config);

        return ToResponse(price);
    }

    public async Task<PriceBreakdown> PriceBookingAsync(BookingDetailsDto booking, QuotationPricingRequest inputs, CancellationToken cancellationToken)
    {
        var distance = inputs.DistanceKm ?? booking.EstimatedDistanceKm
                       ?? throw new RequestValidationException(nameof(inputs.DistanceKm),
                           "Distance is required because the booking has no estimated distance.");

        var config = await LoadConfigurationAsync(booking.VehicleTypeId, booking.PickupCityId, booking.DeliveryCityId, cancellationToken);
        return PricingCalculator.Calculate(new PricingRequestContext(
            distance, booking.TotalWeightKg, inputs.IncludeLoading, inputs.IncludeUnloading, inputs.WaitingHours, inputs.TollAmount,
            inputs.IsNightMovement, booking.RequiresSpecialHandling, inputs.DiscountAmount, inputs.ManualAdjustment,
            inputs.ManualAdjustmentReason), config);
    }

    public Task<PricingConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManagePricing);
        return _pricing.GetConfigurationAsync(cancellationToken);
    }

    public Task<int> SaveVehiclePricingAsync(int? id, SaveVehiclePricingRequest request, CancellationToken cancellationToken) =>
        SaveAsync("VehiclePricing", id, request, () => _pricing.SaveVehiclePricingAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    public Task<int> SaveDistancePricingAsync(int? id, SaveDistancePricingRequest request, CancellationToken cancellationToken) =>
        SaveAsync("DistancePricing", id, request, () => _pricing.SaveDistancePricingAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    public Task<int> SaveAdditionalChargeAsync(int? id, SaveAdditionalChargeRequest request, CancellationToken cancellationToken) =>
        SaveAsync("AdditionalCharge", id, request, () => _pricing.SaveAdditionalChargeAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    public Task<int> SavePricingRuleAsync(int? id, SavePricingRuleRequest request, CancellationToken cancellationToken) =>
        SaveAsync("PricingRule", id, request, () => _pricing.SavePricingRuleAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    public Task<int> SaveTaxRateAsync(int? id, SaveTaxRateRequest request, CancellationToken cancellationToken) =>
        SaveAsync("TaxRate", id, request, () => _pricing.SaveTaxRateAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    public Task<int> SaveCommissionRuleAsync(int? id, SaveCommissionRuleRequest request, CancellationToken cancellationToken) =>
        SaveAsync("CommissionRule", id, request, () => _pricing.SaveCommissionRuleAsync(id, request, _access.User.UserId, cancellationToken), cancellationToken);

    private async Task<int> SaveAsync(string entity, int? id, object request, Func<Task<int>> save, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManagePricing);
        var savedId = await save();
        await _audit.LogAsync(id is null ? $"{entity}Created" : $"{entity}Updated", entity, savedId, newValue: request, cancellationToken: cancellationToken);
        return savedId;
    }

    private async Task<PricingConfiguration> LoadConfigurationAsync(int vehicleTypeId, int? pickupCityId, int? deliveryCityId,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var rate = await _pricing.GetVehiclePricingAsync(vehicleTypeId, now, cancellationToken)
                   ?? throw new BusinessRuleException(ErrorCodes.PricingNotConfigured, "Pricing is not configured for this vehicle type.");
        var slabs = await _pricing.GetDistanceSlabsAsync(vehicleTypeId, cancellationToken);
        var charges = await _pricing.GetAdditionalChargesAsync(vehicleTypeId, cancellationToken);
        var rules = pickupCityId is not null && deliveryCityId is not null
            ? await _pricing.GetApplicableRulesAsync(vehicleTypeId, pickupCityId.Value, deliveryCityId.Value, now, cancellationToken)
            : [];
        var tax = await _pricing.GetTaxRateAsync(ChargeCodes.GoodsTransportTax, now, cancellationToken);

        return new PricingConfiguration(
            new VehicleRateCard(rate.VehicleTypeId, rate.BaseFare, rate.MinimumFare, rate.PerKmRate, rate.PerKgRate, rate.FreeWaitingHours),
            slabs.Select(s => new DistanceSlab(s.FromKm, s.ToKm, s.RatePerKm)).ToList(),
            charges.Select(c => new AdditionalChargeRate(c.ChargeCode, c.Name, c.CalculationType, c.Amount)).ToList(),
            rules.Select(r => new PricingAdjustmentRule(r.Name, r.AdjustmentType, r.AdjustmentValue, r.Priority)).ToList(),
            tax?.RatePercent ?? 0);
    }

    internal static PriceEstimateResponse ToResponse(PriceBreakdown price) =>
        new(price.DistanceKm, price.SubTotal, price.TaxPercent, price.TaxAmount, price.TotalAmount,
            price.Lines.Select(l => new PriceLineDto(l.ChargeCode, l.Description, l.Quantity, l.UnitRate, l.Amount)).ToList());
}
