using ProCargo.Domain.Constants;
using ProCargo.Domain.Entities;
using ProCargo.Domain.Pricing;

namespace ProCargo.UnitTests.Domain;

public sealed class PricingCalculatorTests
{
    private static PricingConfiguration Config(decimal minimumFare = 0, IReadOnlyList<PricingAdjustmentRule>? rules = null) => new(
        new VehicleRateCard(VehicleTypeId: 5, BaseFare: 1000, MinimumFare: minimumFare, PerKmRate: 20, PerKgRate: 0, FreeWaitingHours: 2),
        [new DistanceSlab(0, 100, 25), new DistanceSlab(100, null, 18)],
        [
            new AdditionalChargeRate(ChargeCodes.Loading, "Loading", "Flat", 500),
            new AdditionalChargeRate(ChargeCodes.Unloading, "Unloading", "Flat", 500),
            new AdditionalChargeRate(ChargeCodes.Waiting, "Waiting", "PerHour", 200),
            new AdditionalChargeRate(ChargeCodes.Night, "Night movement", "Percentage", 10)
        ],
        rules ?? [],
        TaxPercent: 5);

    private static PricingRequestContext Request(decimal distance = 150, decimal waiting = 5, bool night = true, decimal discount = 440,
        decimal manual = 0) =>
        new(distance, 2000, IncludeLoading: true, IncludeUnloading: true, waiting, TollAmount: 0, night, RequiresSpecialHandling: false,
            discount, manual, manual == 0 ? null : "Goodwill");

    [Fact]
    public void Calculates_full_breakdown_with_tapered_distance_slabs()
    {
        var price = PricingCalculator.Calculate(Request(), Config());

        Assert.Equal(3400m, price.DistanceCharge);          // 100 km x 25 + 50 km x 18
        Assert.Equal(1000m, price.BaseAmount);
        Assert.Equal(500m, price.LoadingCharge);
        Assert.Equal(500m, price.UnloadingCharge);
        Assert.Equal(600m, price.WaitingCharge);            // (5 - 2 free) h x 200
        Assert.Equal(440m, price.NightCharge);              // 10% of freight 4,400
        Assert.Equal(440m, price.DiscountAmount);
        Assert.Equal(6000m, price.SubTotal);
        Assert.Equal(300m, price.TaxAmount);
        Assert.Equal(6300m, price.TotalAmount);
    }

    [Fact]
    public void Subtotal_always_equals_sum_of_lines()
    {
        var price = PricingCalculator.Calculate(Request(manual: 125.5m), Config(rules: [new PricingAdjustmentRule("Festive", "Percentage", 5, 1)]));
        Assert.Equal(price.SubTotal, price.Lines.Sum(l => l.Amount));
    }

    [Fact]
    public void Applies_minimum_fare_top_up()
    {
        var price = PricingCalculator.Calculate(Request(distance: 5, waiting: 0, night: false, discount: 0), Config(minimumFare: 5000));

        Assert.Contains(price.Lines, l => l.ChargeCode == ChargeCodes.MinimumFareTopUp);
        Assert.Equal(5000m, price.SubTotal);
    }

    [Fact]
    public void Discount_cannot_exceed_price()
    {
        var price = PricingCalculator.Calculate(Request(discount: 1_000_000), Config());
        Assert.Equal(0m, price.SubTotal);
        Assert.Equal(0m, price.TotalAmount);
    }

    [Fact]
    public void Waiting_within_free_hours_is_not_charged()
    {
        var price = PricingCalculator.Calculate(Request(waiting: 2), Config());
        Assert.Equal(0m, price.WaitingCharge);
        Assert.DoesNotContain(price.Lines, l => l.ChargeCode == ChargeCodes.Waiting);
    }

    [Fact]
    public void Flat_and_percentage_rules_are_applied_in_priority_order()
    {
        var rules = new[] { new PricingAdjustmentRule("Hill route", "Flat", 300, 2), new PricingAdjustmentRule("Peak", "Percentage", 10, 1) };
        var price = PricingCalculator.Calculate(Request(night: false, discount: 0, waiting: 0), Config(rules: rules));

        var ruleLines = price.Lines.Where(l => l.ChargeCode == ChargeCodes.RuleAdjustment).ToList();
        Assert.Equal("Peak", ruleLines[0].Description);
        Assert.Equal(440m, ruleLines[0].Amount);
        Assert.Equal(300m, ruleLines[1].Amount);
    }

    [Fact]
    public void Rejects_non_positive_distance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PricingCalculator.Calculate(Request(distance: 0), Config()));
    }
}
