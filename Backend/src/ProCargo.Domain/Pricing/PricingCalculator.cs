using ProCargo.Domain.Constants;
using ProCargo.Domain.Entities;

namespace ProCargo.Domain.Pricing;

/// <summary>What is being priced. Distances in km, weights in kg, money in INR.</summary>
public sealed record PricingRequestContext(
    decimal DistanceKm,
    decimal WeightKg,
    bool IncludeLoading,
    bool IncludeUnloading,
    decimal WaitingHours,
    decimal TollAmount,
    bool IsNightMovement,
    bool RequiresSpecialHandling,
    decimal DiscountAmount,
    decimal ManualAdjustment,
    string? ManualAdjustmentReason);

/// <summary>All configuration needed to price one request (loaded from the fin.* pricing tables).</summary>
public sealed record PricingConfiguration(
    VehicleRateCard RateCard,
    IReadOnlyList<DistanceSlab> DistanceSlabs,
    IReadOnlyList<AdditionalChargeRate> AdditionalCharges,
    IReadOnlyList<PricingAdjustmentRule> Rules,
    decimal TaxPercent);

public sealed record PriceLine(string ChargeCode, string Description, decimal Quantity, decimal UnitRate, decimal Amount, int SortOrder);

/// <summary>Result of a pricing calculation. SubTotal always equals the sum of <see cref="Lines"/>.</summary>
public sealed record PriceBreakdown(
    decimal DistanceKm,
    decimal BaseAmount,
    decimal DistanceCharge,
    decimal LoadingCharge,
    decimal UnloadingCharge,
    decimal WaitingCharge,
    decimal TollCharge,
    decimal NightCharge,
    decimal SpecialHandlingCharge,
    decimal AdjustmentAmount,
    decimal DiscountAmount,
    decimal SubTotal,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    IReadOnlyList<PriceLine> Lines);

/// <summary>
/// Pure, deterministic pricing engine. No I/O: configuration is passed in, so the same calculation is used by the
/// public estimate, the quotation builder and the unit tests.
///
///   freight      = base fare + weight charge + distance charge (tapered slabs)
///   surcharges   = loading + unloading + billable waiting hours + tolls + night % + special handling %
///   adjustments  = pricing rules (% of freight or flat) + manual adjustment + minimum-fare top-up
///   sub-total    = freight + surcharges + adjustments - discount
///   total        = sub-total + tax
/// </summary>
public static class PricingCalculator
{
    public static PriceBreakdown Calculate(PricingRequestContext request, PricingConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);

        if (request.DistanceKm <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Distance must be greater than zero.");
        if (request.WeightKg < 0) throw new ArgumentOutOfRangeException(nameof(request), "Weight cannot be negative.");

        var lines = new List<PriceLine>();
        var sort = 0;
        var card = config.RateCard;

        // ---- freight ----
        var baseFare = Round(card.BaseFare);
        lines.Add(new PriceLine(ChargeCodes.Base, "Base fare", 1, baseFare, baseFare, ++sort));

        var weightCharge = Round(request.WeightKg * card.PerKgRate);
        if (weightCharge > 0)
        {
            lines.Add(new PriceLine(ChargeCodes.Weight, $"Weight charge ({request.WeightKg:0.##} kg)", request.WeightKg, card.PerKgRate, weightCharge, ++sort));
        }

        var distanceCharge = 0m;
        foreach (var (km, rate, label) in SplitDistance(request.DistanceKm, config.DistanceSlabs, card.PerKmRate))
        {
            var amount = Round(km * rate);
            distanceCharge += amount;
            lines.Add(new PriceLine(ChargeCodes.Distance, label, km, rate, amount, ++sort));
        }

        var freight = baseFare + weightCharge + distanceCharge;

        // ---- surcharges ----
        var loading = request.IncludeLoading ? ChargeFor(ChargeCodes.Loading, 1, freight, config, lines, ref sort) : 0;
        var unloading = request.IncludeUnloading ? ChargeFor(ChargeCodes.Unloading, 1, freight, config, lines, ref sort) : 0;

        var billableWaitingHours = Math.Max(0, request.WaitingHours - card.FreeWaitingHours);
        var waiting = billableWaitingHours > 0 ? ChargeFor(ChargeCodes.Waiting, billableWaitingHours, freight, config, lines, ref sort) : 0;

        var toll = Round(Math.Max(0, request.TollAmount));
        if (toll > 0)
        {
            lines.Add(new PriceLine(ChargeCodes.Toll, "Toll charges", 1, toll, toll, ++sort));
        }

        var night = request.IsNightMovement ? ChargeFor(ChargeCodes.Night, 1, freight, config, lines, ref sort) : 0;
        var special = request.RequiresSpecialHandling ? ChargeFor(ChargeCodes.SpecialHandling, 1, freight, config, lines, ref sort) : 0;

        // ---- adjustments ----
        var adjustments = 0m;
        foreach (var rule in config.Rules.OrderBy(r => r.Priority))
        {
            var amount = string.Equals(rule.AdjustmentType, "Percentage", StringComparison.OrdinalIgnoreCase)
                ? Round(freight * rule.AdjustmentValue / 100m)
                : Round(rule.AdjustmentValue);

            if (amount == 0) continue;
            adjustments += amount;
            lines.Add(new PriceLine(ChargeCodes.RuleAdjustment, rule.Name, 1, amount, amount, ++sort));
        }

        if (request.ManualAdjustment != 0)
        {
            var manual = Round(request.ManualAdjustment);
            adjustments += manual;
            lines.Add(new PriceLine(ChargeCodes.ManualAdjustment, request.ManualAdjustmentReason ?? "Adjustment", 1, manual, manual, ++sort));
        }

        var beforeDiscount = freight + loading + unloading + waiting + toll + night + special + adjustments;

        if (beforeDiscount < card.MinimumFare)
        {
            var topUp = Round(card.MinimumFare - beforeDiscount);
            adjustments += topUp;
            beforeDiscount += topUp;
            lines.Add(new PriceLine(ChargeCodes.MinimumFareTopUp, "Minimum fare top-up", 1, topUp, topUp, ++sort));
        }

        // ---- discount ----
        var discount = Round(Math.Clamp(request.DiscountAmount, 0, beforeDiscount));
        if (discount > 0)
        {
            lines.Add(new PriceLine(ChargeCodes.Discount, "Discount", 1, -discount, -discount, ++sort));
        }

        var subTotal = Round(beforeDiscount - discount);
        var taxAmount = Round(subTotal * config.TaxPercent / 100m);

        return new PriceBreakdown(
            DistanceKm: request.DistanceKm,
            BaseAmount: baseFare + weightCharge,
            DistanceCharge: distanceCharge,
            LoadingCharge: loading,
            UnloadingCharge: unloading,
            WaitingCharge: waiting,
            TollCharge: toll,
            NightCharge: night,
            SpecialHandlingCharge: special,
            AdjustmentAmount: adjustments,
            DiscountAmount: discount,
            SubTotal: subTotal,
            TaxPercent: config.TaxPercent,
            TaxAmount: taxAmount,
            TotalAmount: subTotal + taxAmount,
            Lines: lines);
    }

    /// <summary>
    /// Splits the distance across the configured slabs. Any distance not covered by a slab is charged at the
    /// vehicle's default per-km rate.
    /// </summary>
    internal static IEnumerable<(decimal Km, decimal Rate, string Label)> SplitDistance(
        decimal distanceKm, IReadOnlyList<DistanceSlab> slabs, decimal defaultRate)
    {
        if (slabs.Count == 0)
        {
            yield return (distanceKm, defaultRate, $"Distance charge ({distanceKm:0.##} km)");
            yield break;
        }

        var covered = 0m;
        foreach (var slab in slabs.OrderBy(s => s.FromKm))
        {
            if (distanceKm <= slab.FromKm) break;

            var upper = slab.ToKm.HasValue ? Math.Min(distanceKm, slab.ToKm.Value) : distanceKm;
            var km = upper - Math.Max(slab.FromKm, covered);
            if (km <= 0) continue;

            covered = upper;
            var range = slab.ToKm.HasValue ? $"{slab.FromKm:0.##}-{slab.ToKm:0.##} km" : $"above {slab.FromKm:0.##} km";
            yield return (km, slab.RatePerKm, $"Distance {range} ({km:0.##} km)");
        }

        if (covered < distanceKm)
        {
            var remaining = distanceKm - covered;
            yield return (remaining, defaultRate, $"Distance beyond slabs ({remaining:0.##} km)");
        }
    }

    private static decimal ChargeFor(string code, decimal quantity, decimal freight, PricingConfiguration config, List<PriceLine> lines, ref int sort)
    {
        var charge = config.AdditionalCharges.FirstOrDefault(c => string.Equals(c.ChargeCode, code, StringComparison.OrdinalIgnoreCase));
        if (charge is null) return 0;

        var (qty, rate, amount) = charge.CalculationType switch
        {
            "Percentage" => (1m, Round(freight * charge.Amount / 100m), Round(freight * charge.Amount / 100m)),
            "PerHour" or "PerKm" => (quantity, charge.Amount, Round(quantity * charge.Amount)),
            _ => (1m, charge.Amount, Round(charge.Amount))
        };

        if (amount == 0) return 0;

        var description = charge.CalculationType switch
        {
            "Percentage" => $"{charge.Name} ({charge.Amount:0.##}% of freight)",
            "PerHour" => $"{charge.Name} ({quantity:0.##} h)",
            "PerKm" => $"{charge.Name} ({quantity:0.##} km)",
            _ => charge.Name
        };

        lines.Add(new PriceLine(charge.ChargeCode, description, qty, rate, amount, ++sort));
        return amount;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
