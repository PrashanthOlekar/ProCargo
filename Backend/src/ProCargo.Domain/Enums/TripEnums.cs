namespace ProCargo.Domain.Enums;

/// <summary>Trip lifecycle. Ids mirror mst.TripStatus.</summary>
public enum TripStatus
{
    Scheduled = 1,
    PickupVerified = 2,
    InTransit = 3,
    Delivered = 4,
    PodUploaded = 5,
    Completed = 6,
    Closed = 7,
    Cancelled = 8,
    OnHold = 9,
    Exception = 10
}

/// <summary>OTP verification step of a trip.</summary>
public enum VerificationType
{
    Pickup = 1,
    Delivery = 2
}

/// <summary>Source of a tracking point. Ids mirror core.TrackingProvider.</summary>
public enum TrackingProviderType
{
    DriverApp = 1,
    Manual = 2,
    GpsDevice = 3
}

/// <summary>Kind of trip assignment history row.</summary>
public enum AssignmentType
{
    Vehicle = 1,
    Driver = 2
}
