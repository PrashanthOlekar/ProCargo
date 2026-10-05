using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

// ---------- Customers ----------

public sealed class CustomerSearchRequest : PagedRequest
{
    public CustomerType? CustomerType { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class UpdateCustomerRequest
{
    public CustomerType CustomerType { get; set; } = CustomerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SaveCustomerAddressRequest
{
    public string Label { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public int CityId { get; set; }
    public string Pincode { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class SaveCustomerContactRequest
{
    public string ContactName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Designation { get; set; }
    public bool IsPrimary { get; set; }
}

// ---------- Owners ----------

public sealed class OwnerSearchRequest : PagedRequest
{
    public VerificationStatus? VerificationStatus { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class UpdateOwnerRequest
{
    public OwnerType OwnerType { get; set; } = OwnerType.Individual;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Optional: only sent when the PAN is being set or changed (stored encrypted).</summary>
    public string? PanNumber { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed class SaveOwnerBusinessRequest
{
    public string BusinessName { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public int? FleetSize { get; set; }
}

public sealed class SaveOwnerAddressRequest
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public int CityId { get; set; }
    public string Pincode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = true;
}

public sealed class CreateBankAccountRequest
{
    public string AccountHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string IfscCode { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

/// <summary>Staff decision on an owner, driver, vehicle, bank account or document.</summary>
public sealed class VerificationDecisionRequest
{
    public VerificationStatus Status { get; set; }
    public string? Remarks { get; set; }
}

// ---------- Drivers ----------

public sealed class DriverSearchRequest : PagedRequest
{
    public long? OwnerId { get; set; }
    public VerificationStatus? VerificationStatus { get; set; }
    public DriverAvailabilityStatus? AvailabilityStatus { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreateDriverRequest
{
    /// <summary>Staff only: the owner employing the driver. Owners always create drivers for themselves.</summary>
    public long? OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseClass { get; set; } = string.Empty;
    public DateOnly? LicenseIssueDate { get; set; }
    public DateOnly LicenseExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
}

public sealed class UpdateDriverRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AlternatePhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SaveDriverLicenseRequest
{
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseClass { get; set; } = string.Empty;
    public DateOnly? IssueDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
}

public sealed class SetDriverAvailabilityRequest
{
    public DriverAvailabilityStatus Status { get; set; }
    public string? Reason { get; set; }
}

// ---------- Vehicles ----------

public sealed class VehicleSearchRequest : PagedRequest
{
    public long? OwnerId { get; set; }
    public int? VehicleTypeId { get; set; }
    public VerificationStatus? VerificationStatus { get; set; }
    public bool? IsAvailable { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreateVehicleRequest
{
    /// <summary>Staff only. Owners always add vehicles to their own fleet.</summary>
    public long? OwnerId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public int VehicleTypeId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? ManufactureYear { get; set; }
    public decimal CapacityKg { get; set; }
    public string? PermitNumber { get; set; }
    public DateOnly? PermitExpiryDate { get; set; }
    public string? InsuranceNumber { get; set; }
    public DateOnly? InsuranceExpiryDate { get; set; }
    public DateOnly? FitnessExpiryDate { get; set; }
    public DateOnly? PucExpiryDate { get; set; }
}

public sealed class UpdateVehicleRequest
{
    public int VehicleTypeId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? ManufactureYear { get; set; }
    public decimal CapacityKg { get; set; }
    public string? PermitNumber { get; set; }
    public DateOnly? PermitExpiryDate { get; set; }
    public string? InsuranceNumber { get; set; }
    public DateOnly? InsuranceExpiryDate { get; set; }
    public DateOnly? FitnessExpiryDate { get; set; }
    public DateOnly? PucExpiryDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SetVehicleAvailabilityRequest
{
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
}

// ---------- Documents ----------

/// <summary>An uploaded file as received by a controller (stream + client-supplied metadata).</summary>
public sealed record FileUpload(Stream Content, string FileName, string ContentType, long Length);

public sealed class UploadDocumentRequest
{
    public int DocumentTypeId { get; set; }
    public string? DocumentNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
