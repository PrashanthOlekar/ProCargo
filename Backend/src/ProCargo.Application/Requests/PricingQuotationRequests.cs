using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

/// <summary>Public / customer price estimate (no booking needed).</summary>
public sealed class PriceEstimateRequest
{
    public int VehicleTypeId { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal WeightKg { get; set; }
    public int? PickupCityId { get; set; }
    public int? DeliveryCityId { get; set; }
    public bool IncludeLoading { get; set; }
    public bool IncludeUnloading { get; set; }
    public bool IsNightMovement { get; set; }
    public bool RequiresSpecialHandling { get; set; }
}

/// <summary>Operations inputs used to price a booking into a quotation.</summary>
public class QuotationPricingRequest
{
    public decimal? DistanceKm { get; set; }
    public bool IncludeLoading { get; set; } = true;
    public bool IncludeUnloading { get; set; } = true;
    public decimal WaitingHours { get; set; }
    public decimal TollAmount { get; set; }
    public bool IsNightMovement { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ManualAdjustment { get; set; }
    public string? ManualAdjustmentReason { get; set; }
}

public sealed class PreviewQuotationRequest : QuotationPricingRequest
{
    public long BookingId { get; set; }
}

public sealed class CreateQuotationRequest : QuotationPricingRequest
{
    public long BookingId { get; set; }
    public int? ValidityHours { get; set; }
    public string? Notes { get; set; }
    public bool SendImmediately { get; set; }
}

public sealed class UpdateQuotationRequest : QuotationPricingRequest
{
    public int? ValidityHours { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class QuotationSearchRequest : PagedRequest
{
    public long? BookingId { get; set; }
    public QuotationStatus? Status { get; set; }

    /// <summary>Staff only.</summary>
    public long? CustomerId { get; set; }
}

public sealed class RejectQuotationRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class WithdrawQuotationRequest
{
    public string Reason { get; set; } = string.Empty;
}

// ---------- pricing configuration (Finance) ----------

public sealed class SaveVehiclePricingRequest
{
    public int VehicleTypeId { get; set; }
    public decimal BaseFare { get; set; }
    public decimal MinimumFare { get; set; }
    public decimal PerKmRate { get; set; }
    public decimal PerKgRate { get; set; }
    public decimal FreeWaitingHours { get; set; } = 2;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveDistancePricingRequest
{
    public int VehicleTypeId { get; set; }
    public decimal FromKm { get; set; }
    public decimal? ToKm { get; set; }
    public decimal RatePerKm { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveAdditionalChargeRequest
{
    public string ChargeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? VehicleTypeId { get; set; }
    public string CalculationType { get; set; } = "Flat";
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SavePricingRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public int? VehicleTypeId { get; set; }
    public int? StateId { get; set; }
    public int? CityId { get; set; }
    public int? PickupCityId { get; set; }
    public int? DeliveryCityId { get; set; }
    public string AdjustmentType { get; set; } = "Percentage";
    public decimal AdjustmentValue { get; set; }
    public int Priority { get; set; } = 100;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveTaxRateRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveCommissionRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public int? VehicleTypeId { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal MinimumCommission { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
