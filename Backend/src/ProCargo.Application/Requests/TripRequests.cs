using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

public sealed class CreateTripRequest
{
    public long BookingId { get; set; }
    public long VehicleId { get; set; }
    public long DriverId { get; set; }
    public DateTime PlannedPickupDateUtc { get; set; }
    public DateTime PlannedDeliveryDateUtc { get; set; }
}

public sealed class ReassignVehicleRequest
{
    public long VehicleId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReassignDriverRequest
{
    public long DriverId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class TripSearchRequest : PagedRequest
{
    public TripStatus? Status { get; set; }
    public bool ActiveOnly { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }

    /// <summary>Staff only filters.</summary>
    public long? CustomerId { get; set; }
    public long? OwnerId { get; set; }
    public long? DriverId { get; set; }
}

public sealed class TripReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class StartTripRequest
{
    public string? Remarks { get; set; }
}

public sealed class VerifyOtpRequest
{
    public string Otp { get; set; } = string.Empty;
    public string? ReceiverName { get; set; }
    public int? Odometer { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class LocationPointRequest
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime RecordedDateUtc { get; set; }
    public decimal? SpeedKmph { get; set; }
    public decimal? Heading { get; set; }
    public decimal? Accuracy { get; set; }
}

public sealed class AddLocationsRequest
{
    public List<LocationPointRequest> Points { get; set; } = [];
}

public sealed class UploadProofOfDeliveryRequest
{
    public string ReceiverName { get; set; } = string.Empty;
    public string? ReceiverPhone { get; set; }
    public string? Remarks { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}
