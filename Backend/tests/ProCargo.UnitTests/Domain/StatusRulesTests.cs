using ProCargo.Domain.DomainRules;
using ProCargo.Domain.Enums;
using ProCargo.Domain.Exceptions;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.UnitTests.Domain;

public sealed class StatusRulesTests
{
    [Theory]
    [InlineData(TripStatus.Scheduled, TripStatus.PickupVerified, true)]
    [InlineData(TripStatus.Scheduled, TripStatus.Delivered, false)]
    [InlineData(TripStatus.InTransit, TripStatus.Delivered, true)]
    [InlineData(TripStatus.Delivered, TripStatus.Cancelled, false)]
    [InlineData(TripStatus.Closed, TripStatus.InTransit, false)]
    public void Trip_transitions_follow_the_lifecycle(TripStatus from, TripStatus to, bool allowed)
    {
        Assert.Equal(allowed, StatusRules.Trip.CanTransition(from, to));
    }

    [Fact]
    public void Invalid_transition_throws_with_entity_specific_code()
    {
        var ex = Assert.Throws<InvalidStatusTransitionException>(() =>
            StatusRules.Settlement.EnsureCanTransition(SettlementStatus.Pending, SettlementStatus.Completed));
        Assert.Equal("SETTLEMENT_INVALID_TRANSITION", ex.ErrorCode);
    }

    [Fact]
    public void Settlement_must_be_approved_before_processing()
    {
        Assert.False(StatusRules.Settlement.CanTransition(SettlementStatus.Pending, SettlementStatus.Processing));
        Assert.True(StatusRules.Settlement.CanTransition(SettlementStatus.Approved, SettlementStatus.Processing));
    }

    [Fact]
    public void Tracking_is_accepted_only_between_pickup_and_delivery()
    {
        Assert.False(StatusRules.IsTripTrackable(TripStatus.Scheduled));
        Assert.True(StatusRules.IsTripTrackable(TripStatus.InTransit));
        Assert.False(StatusRules.IsTripTrackable(TripStatus.Delivered));
    }
}

public sealed class IndianFormatsTests
{
    [Theory]
    [InlineData("98450 12345", "+919845012345")]
    [InlineData("+91 98450-12345", "+919845012345")]
    [InlineData("919845012345", "+919845012345")]
    [InlineData("09845012345", "+919845012345")]
    public void Normalizes_indian_mobile_numbers_to_e164(string input, string expected)
    {
        Assert.Equal(expected, IndianFormats.NormalizePhone(input));
    }

    [Fact]
    public void Masks_phone_numbers()
    {
        Assert.Equal("+91******2345", IndianFormats.MaskPhone("+919845012345"));
    }

    [Theory]
    [InlineData("KA 25 AB 1234", true)]
    [InlineData("ka-01-c-1234", true)]
    [InlineData("22BH1234AA", true)]
    [InlineData("1234", false)]
    public void Validates_vehicle_registration_numbers(string number, bool valid)
    {
        Assert.Equal(valid, IndianFormats.IsValidVehicleNumber(number));
    }

    [Theory]
    [InlineData("29ABCDE1234F1Z5", true)]
    [InlineData("29ABCDE1234F1Z", false)]
    public void Validates_gstin(string gst, bool valid)
    {
        Assert.Equal(valid, IndianFormats.IsValidGst(gst));
    }

    [Theory]
    [InlineData("ABCDE1234F", true)]
    [InlineData("ABCD1234F", false)]
    public void Validates_pan(string pan, bool valid)
    {
        Assert.Equal(valid, IndianFormats.IsValidPan(pan));
    }

    [Fact]
    public void Haversine_distance_bengaluru_to_mysuru_is_about_125_km()
    {
        var km = new GeoPoint(12.9716m, 77.5946m).DistanceKmTo(new GeoPoint(12.2958m, 76.6394m));
        Assert.InRange(km, 120, 130);
    }
}
