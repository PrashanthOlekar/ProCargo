namespace ProCargo.Domain.Entities;

/// <summary>Rate card of a vehicle type (fin.VehiclePricing).</summary>
public sealed record VehicleRateCard(
    int VehicleTypeId,
    decimal BaseFare,
    decimal MinimumFare,
    decimal PerKmRate,
    decimal PerKgRate,
    decimal FreeWaitingHours);

/// <summary>Per-km rate applied to the part of the distance inside [FromKm, ToKm) (fin.DistancePricing).</summary>
public sealed record DistanceSlab(decimal FromKm, decimal? ToKm, decimal RatePerKm);

/// <summary>Configurable extra charge (fin.AdditionalCharge). CalculationType: Flat | PerHour | PerKm | Percentage.</summary>
public sealed record AdditionalChargeRate(string ChargeCode, string Name, string CalculationType, decimal Amount);

/// <summary>Location / route / season adjustment (fin.PricingRule). AdjustmentType: Percentage | Flat.</summary>
public sealed record PricingAdjustmentRule(string Name, string AdjustmentType, decimal AdjustmentValue, int Priority);

/// <summary>Minimal identity used by the password hasher. Persistence lives in sec.[User].</summary>
public sealed class UserAccount
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
}
