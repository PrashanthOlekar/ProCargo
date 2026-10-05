using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class PricingRepository : IPricingRepository
{
    private readonly StoredProcedureExecutor _sp;

    public PricingRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<VehiclePricingRow?> GetVehiclePricingAsync(int vehicleTypeId, DateTime atUtc, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<VehiclePricingRow>(
            $"EXEC fin.usp_Pricing_GetVehiclePricing @VehicleTypeId={vehicleTypeId}, @AtUtc={atUtc}", cancellationToken);

    public Task<IReadOnlyList<DistanceSlabRow>> GetDistanceSlabsAsync(int vehicleTypeId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<DistanceSlabRow>($"EXEC fin.usp_Pricing_GetDistanceSlabs @VehicleTypeId={vehicleTypeId}", cancellationToken);

    public Task<IReadOnlyList<AdditionalChargeRow>> GetAdditionalChargesAsync(int vehicleTypeId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<AdditionalChargeRow>($"EXEC fin.usp_Pricing_GetAdditionalCharges @VehicleTypeId={vehicleTypeId}", cancellationToken);

    public Task<IReadOnlyList<PricingRuleRow>> GetApplicableRulesAsync(int vehicleTypeId, int pickupCityId, int deliveryCityId, DateTime atUtc,
        CancellationToken cancellationToken) =>
        _sp.QueryAsync<PricingRuleRow>($"""
            EXEC fin.usp_Pricing_GetApplicableRules @VehicleTypeId={vehicleTypeId}, @PickupCityId={pickupCityId},
                @DeliveryCityId={deliveryCityId}, @AtUtc={atUtc}
            """, cancellationToken);

    public Task<TaxRateRow?> GetTaxRateAsync(string code, DateTime atUtc, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<TaxRateRow>($"EXEC fin.usp_Pricing_GetTaxRate @Code={code}, @AtUtc={atUtc}", cancellationToken);

    public Task<CommissionRuleRow?> GetCommissionRuleAsync(int vehicleTypeId, DateTime atUtc, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<CommissionRuleRow>(
            $"EXEC fin.usp_Pricing_GetCommissionRule @VehicleTypeId={vehicleTypeId}, @AtUtc={atUtc}", cancellationToken);

    public async Task<PricingConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var vehicle = await _sp.QueryAsync<VehiclePricingDto>($"EXEC fin.usp_VehiclePricing_GetAll", cancellationToken);
        var distance = await _sp.QueryAsync<DistancePricingDto>($"EXEC fin.usp_DistancePricing_GetAll", cancellationToken);
        var charges = await _sp.QueryAsync<AdditionalChargeDto>($"EXEC fin.usp_AdditionalCharge_GetAll", cancellationToken);
        var rules = await _sp.QueryAsync<PricingRuleDto>($"EXEC fin.usp_PricingRule_GetAll", cancellationToken);
        var taxes = await _sp.QueryAsync<TaxRateDto>($"EXEC fin.usp_TaxRate_GetAll", cancellationToken);
        var commissions = await _sp.QueryAsync<CommissionRuleDto>($"EXEC fin.usp_CommissionRule_GetAll", cancellationToken);
        return new PricingConfigurationResponse(vehicle, distance, charges, rules, taxes, commissions);
    }

    public async Task<int> SaveVehiclePricingAsync(int? id, SaveVehiclePricingRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_VehiclePricing_Save @VehiclePricingId={id}, @VehicleTypeId={r.VehicleTypeId}, @BaseFare={r.BaseFare},
                @MinimumFare={r.MinimumFare}, @PerKmRate={r.PerKmRate}, @PerKgRate={SqlParams.Decimal("PerKgRate", r.PerKgRate, 10, 4)},
                @FreeWaitingHours={r.FreeWaitingHours}, @EffectiveFromUtc={r.EffectiveFromUtc}, @EffectiveToUtc={r.EffectiveToUtc},
                @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> SaveDistancePricingAsync(int? id, SaveDistancePricingRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_DistancePricing_Save @DistancePricingId={id}, @VehicleTypeId={r.VehicleTypeId}, @FromKm={r.FromKm},
                @ToKm={r.ToKm}, @RatePerKm={r.RatePerKm}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> SaveAdditionalChargeAsync(int? id, SaveAdditionalChargeRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_AdditionalCharge_Save @AdditionalChargeId={id}, @ChargeCode={r.ChargeCode.Trim().ToUpperInvariant()},
                @Name={r.Name.Trim()}, @VehicleTypeId={r.VehicleTypeId}, @CalculationType={r.CalculationType}, @Amount={r.Amount},
                @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> SavePricingRuleAsync(int? id, SavePricingRuleRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_PricingRule_Save @PricingRuleId={id}, @Name={r.Name.Trim()}, @VehicleTypeId={r.VehicleTypeId},
                @StateId={r.StateId}, @CityId={r.CityId}, @PickupCityId={r.PickupCityId}, @DeliveryCityId={r.DeliveryCityId},
                @AdjustmentType={r.AdjustmentType}, @AdjustmentValue={r.AdjustmentValue}, @Priority={r.Priority},
                @EffectiveFromUtc={r.EffectiveFromUtc}, @EffectiveToUtc={r.EffectiveToUtc}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> SaveTaxRateAsync(int? id, SaveTaxRateRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_TaxRate_Save @TaxRateId={id}, @Code={r.Code.Trim().ToUpperInvariant()}, @Name={r.Name.Trim()},
                @RatePercent={r.RatePercent}, @EffectiveFromUtc={r.EffectiveFromUtc}, @EffectiveToUtc={r.EffectiveToUtc},
                @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }

    public async Task<int> SaveCommissionRuleAsync(int? id, SaveCommissionRuleRequest r, long userId, CancellationToken cancellationToken)
    {
        var result = await _sp.QuerySingleAsync<IdResult>($"""
            EXEC fin.usp_CommissionRule_Save @CommissionRuleId={id}, @Name={r.Name.Trim()}, @VehicleTypeId={r.VehicleTypeId},
                @CommissionPercent={r.CommissionPercent}, @MinimumCommission={r.MinimumCommission},
                @EffectiveFromUtc={r.EffectiveFromUtc}, @EffectiveToUtc={r.EffectiveToUtc}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken);
        return (int)result.Id;
    }
}
