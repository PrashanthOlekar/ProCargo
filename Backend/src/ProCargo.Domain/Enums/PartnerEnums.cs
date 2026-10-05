namespace ProCargo.Domain.Enums;

public enum VerificationStatus
{
    Pending = 1,
    UnderReview = 2,
    Verified = 3,
    Rejected = 4,
    Expired = 5
}

public enum DriverAvailabilityStatus
{
    Available = 1,
    OnTrip = 2,
    OffDuty = 3,
    Unavailable = 4
}

public enum CustomerType
{
    Individual = 1,
    Business = 2
}

public enum OwnerType
{
    Individual = 1,
    FleetBusiness = 2
}

/// <summary>Records that can own KYC / compliance documents.</summary>
public enum DocumentEntityType
{
    Customer = 1,
    Owner = 2,
    Driver = 3,
    Vehicle = 4
}
