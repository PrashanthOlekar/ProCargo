using System.Text.Json.Serialization;
using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

// ---------- pricing configuration rows (fin.usp_Pricing_* / fin.usp_*_GetAll) ----------

public sealed class VehiclePricingRow
{
    public int VehiclePricingId { get; init; }
    public int VehicleTypeId { get; init; }
    public decimal BaseFare { get; init; }
    public decimal MinimumFare { get; init; }
    public decimal PerKmRate { get; init; }
    public decimal PerKgRate { get; init; }
    public decimal FreeWaitingHours { get; init; }
}

public sealed class DistanceSlabRow
{
    public int DistancePricingId { get; init; }
    public int VehicleTypeId { get; init; }
    public decimal FromKm { get; init; }
    public decimal? ToKm { get; init; }
    public decimal RatePerKm { get; init; }
}

public sealed class AdditionalChargeRow
{
    public int AdditionalChargeId { get; init; }
    public string ChargeCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? VehicleTypeId { get; init; }
    public string CalculationType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class PricingRuleRow
{
    public int PricingRuleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AdjustmentType { get; init; } = string.Empty;
    public decimal AdjustmentValue { get; init; }
    public int Priority { get; init; }
}

public sealed class TaxRateRow
{
    public int TaxRateId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal RatePercent { get; init; }
}

public sealed class CommissionRuleRow
{
    public int CommissionRuleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal CommissionPercent { get; init; }
    public decimal MinimumCommission { get; init; }
}

public sealed class VehiclePricingDto
{
    public int VehiclePricingId { get; init; }
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public decimal BaseFare { get; init; }
    public decimal MinimumFare { get; init; }
    public decimal PerKmRate { get; init; }
    public decimal PerKgRate { get; init; }
    public decimal FreeWaitingHours { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DistancePricingDto
{
    public int DistancePricingId { get; init; }
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public decimal FromKm { get; init; }
    public decimal? ToKm { get; init; }
    public decimal RatePerKm { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AdditionalChargeDto
{
    public int AdditionalChargeId { get; init; }
    public string ChargeCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int? VehicleTypeId { get; init; }
    public string? VehicleTypeName { get; init; }
    public string CalculationType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public bool IsActive { get; init; }
}

public sealed class PricingRuleDto
{
    public int PricingRuleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? VehicleTypeId { get; init; }
    public string? VehicleTypeName { get; init; }
    public int? StateId { get; init; }
    public string? StateName { get; init; }
    public int? CityId { get; init; }
    public string? CityName { get; init; }
    public int? PickupCityId { get; init; }
    public string? PickupCityName { get; init; }
    public int? DeliveryCityId { get; init; }
    public string? DeliveryCityName { get; init; }
    public string AdjustmentType { get; init; } = string.Empty;
    public decimal AdjustmentValue { get; init; }
    public int Priority { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public bool IsActive { get; init; }
}

public sealed class TaxRateDto
{
    public int TaxRateId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal RatePercent { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CommissionRuleDto
{
    public int CommissionRuleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int? VehicleTypeId { get; init; }
    public string? VehicleTypeName { get; init; }
    public decimal CommissionPercent { get; init; }
    public decimal MinimumCommission { get; init; }
    public DateTime EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public bool IsActive { get; init; }
}

public sealed record PricingConfigurationResponse(
    IReadOnlyList<VehiclePricingDto> VehiclePricing,
    IReadOnlyList<DistancePricingDto> DistanceSlabs,
    IReadOnlyList<AdditionalChargeDto> AdditionalCharges,
    IReadOnlyList<PricingRuleDto> Rules,
    IReadOnlyList<TaxRateDto> TaxRates,
    IReadOnlyList<CommissionRuleDto> CommissionRules);

/// <summary>A price breakdown returned by estimate / preview endpoints.</summary>
public sealed record PriceLineDto(string ChargeCode, string Description, decimal Quantity, decimal UnitRate, decimal Amount);

public sealed record PriceEstimateResponse(
    decimal DistanceKm,
    decimal SubTotal,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    IReadOnlyList<PriceLineDto> Lines);

// ---------- quotations (core.usp_Quotation_*) ----------

public sealed class QuotationListItemDto : PagedRow
{
    public long QuotationId { get; init; }
    public string QuotationNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int VersionNo { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime ValidityDateUtc { get; init; }
    public int QuotationStatusId { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class QuotationDto
{
    public long QuotationId { get; init; }
    public string QuotationNumber { get; init; } = string.Empty;
    public long BookingId { get; init; }
    public string BookingNumber { get; init; } = string.Empty;
    public long CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;

    [JsonIgnore]
    public long CustomerUserId { get; init; }

    public int BookingStatusId { get; init; }
    public int VersionNo { get; init; }
    public decimal DistanceKm { get; init; }
    public decimal BaseAmount { get; init; }
    public decimal DistanceCharge { get; init; }
    public decimal LoadingCharge { get; init; }
    public decimal UnloadingCharge { get; init; }
    public decimal WaitingCharge { get; init; }
    public decimal TollCharge { get; init; }
    public decimal NightCharge { get; init; }
    public decimal SpecialHandlingCharge { get; init; }
    public decimal AdjustmentAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal SubTotal { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTime ValidityDateUtc { get; init; }
    public int QuotationStatusId { get; init; }
    public string? Notes { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime? SentDateUtc { get; init; }
    public DateTime? RespondedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class QuotationChargeDto
{
    public long QuotationChargeId { get; init; }
    public string ChargeCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitRate { get; init; }
    public decimal Amount { get; init; }
    public int SortOrder { get; init; }
}

public sealed record QuotationDetailsResponse(QuotationDto Quotation, IReadOnlyList<QuotationChargeDto> Charges, IReadOnlyList<string> AvailableActions);
