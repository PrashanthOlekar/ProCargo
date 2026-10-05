using System.Text.Json.Serialization;
using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

// ---------------- Customers (core.usp_Customer*) ----------------

public sealed class CustomerListItemDto : PagedRow
{
    public long CustomerId { get; init; }
    public string CustomerNumber { get; init; } = string.Empty;
    public int CustomerTypeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public int BookingCount { get; init; }
}

public sealed class CustomerDto
{
    public long CustomerId { get; init; }
    public long UserId { get; init; }
    public string CustomerNumber { get; init; } = string.Empty;
    public int CustomerTypeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? GstNumber { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class CustomerAddressDto
{
    public long CustomerAddressId { get; init; }
    public long CustomerId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string? Landmark { get; init; }
    public int CityId { get; init; }
    public string CityName { get; init; } = string.Empty;
    public int StateId { get; init; }
    public string StateName { get; init; } = string.Empty;
    public string Pincode { get; init; } = string.Empty;
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class CustomerContactDto
{
    public long CustomerContactId { get; init; }
    public long CustomerId { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? Designation { get; init; }
    public bool IsPrimary { get; init; }
}

// ---------------- Vehicle owners (core.usp_Owner*) ----------------

public sealed class OwnerListItemDto : PagedRow
{
    public long OwnerId { get; init; }
    public string OwnerNumber { get; init; } = string.Empty;
    public int OwnerTypeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public int VerificationStatusId { get; init; }
    public bool IsActive { get; init; }
    public int VehicleCount { get; init; }
    public int DriverCount { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class OwnerDto
{
    public long OwnerId { get; init; }
    public long UserId { get; init; }
    public string OwnerNumber { get; init; } = string.Empty;
    public int OwnerTypeId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? PanLast4 { get; init; }
    public int VerificationStatusId { get; init; }
    public string? VerificationRemarks { get; init; }
    public DateTime? VerifiedDateUtc { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public string? BusinessName { get; init; }
    public string? GstNumber { get; init; }
    public string? RegistrationNumber { get; init; }
    public int? FleetSize { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class OwnerAddressDto
{
    public long OwnerAddressId { get; init; }
    public long OwnerId { get; init; }
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string? Landmark { get; init; }
    public int CityId { get; init; }
    public string CityName { get; init; } = string.Empty;
    public int StateId { get; init; }
    public string StateName { get; init; } = string.Empty;
    public string Pincode { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}

/// <summary>Bank account as shown in lists: only the last four digits of the account number.</summary>
public sealed class OwnerBankAccountDto
{
    public long OwnerBankAccountId { get; init; }
    public long OwnerId { get; init; }
    public string AccountHolderName { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string AccountNumberLast4 { get; init; } = string.Empty;
    public string IfscCode { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public int VerificationStatusId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

/// <summary>core.usp_OwnerBankAccount_GetSensitive - ciphertext, decrypted only for an audited reveal.</summary>
public sealed class OwnerBankAccountSensitiveRow
{
    public long OwnerBankAccountId { get; init; }
    public string AccountHolderName { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string IfscCode { get; init; } = string.Empty;
    public string AccountNumberEncrypted { get; init; } = string.Empty;
}

public sealed record BankAccountRevealResponse(long OwnerBankAccountId, string AccountHolderName, string BankName, string IfscCode, string AccountNumber);

public sealed record OwnerProfileResponse(OwnerDto Owner, IReadOnlyList<OwnerAddressDto> Addresses, IReadOnlyList<OwnerBankAccountDto> BankAccounts);

// ---------------- Drivers (core.usp_Driver*) ----------------

public sealed class DriverListItemDto : PagedRow
{
    public long DriverId { get; init; }
    public string DriverNumber { get; init; } = string.Empty;
    public long? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? LicenseNumber { get; init; }
    public DateOnly? LicenseExpiryDate { get; init; }
    public int VerificationStatusId { get; init; }
    public int AvailabilityStatusId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class DriverDto
{
    public long DriverId { get; init; }
    public long UserId { get; init; }
    public string DriverNumber { get; init; } = string.Empty;
    public long? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string? AlternatePhoneNumber { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? LicenseNumber { get; init; }
    public string? LicenseClass { get; init; }
    public DateOnly? LicenseIssueDate { get; init; }
    public DateOnly? LicenseExpiryDate { get; init; }
    public string? LicenseIssuingAuthority { get; init; }
    public int VerificationStatusId { get; init; }
    public string? VerificationRemarks { get; init; }
    public DateTime? VerifiedDateUtc { get; init; }
    public int AvailabilityStatusId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class AvailableDriverDto
{
    public long DriverId { get; init; }
    public string DriverNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public long? OwnerId { get; init; }
    public DateOnly LicenseExpiryDate { get; init; }
}

// ---------------- Vehicles (core.usp_Vehicle*) ----------------

public sealed class VehicleListItemDto : PagedRow
{
    public long VehicleId { get; init; }
    public string VehicleNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public decimal CapacityKg { get; init; }
    public int VerificationStatusId { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsActive { get; init; }
    public DateOnly? InsuranceExpiryDate { get; init; }
    public DateOnly? PermitExpiryDate { get; init; }
    public DateOnly? FitnessExpiryDate { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class VehicleDto
{
    public long VehicleId { get; init; }
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public string VehicleNumber { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public string VehicleTypeName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int? ManufactureYear { get; init; }
    public decimal CapacityKg { get; init; }
    public string? PermitNumber { get; init; }
    public DateOnly? PermitExpiryDate { get; init; }
    public string? InsuranceNumber { get; init; }
    public DateOnly? InsuranceExpiryDate { get; init; }
    public DateOnly? FitnessExpiryDate { get; init; }
    public DateOnly? PucExpiryDate { get; init; }
    public int VerificationStatusId { get; init; }
    public string? VerificationRemarks { get; init; }
    public DateTime? VerifiedDateUtc { get; init; }
    public bool IsAvailable { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class AvailableVehicleDto
{
    public long VehicleId { get; init; }
    public string VehicleNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public int VehicleTypeId { get; init; }
    public decimal CapacityKg { get; init; }
    public DateOnly? InsuranceExpiryDate { get; init; }
}

public sealed class ExpiringVehicleDto
{
    public long VehicleId { get; init; }
    public string VehicleNumber { get; init; } = string.Empty;
    public long OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public DateOnly? InsuranceExpiryDate { get; init; }
    public DateOnly? PermitExpiryDate { get; init; }
    public DateOnly? FitnessExpiryDate { get; init; }
    public DateOnly? PucExpiryDate { get; init; }
    public DateOnly? NextExpiryDate { get; init; }
    public int? DaysToNextExpiry { get; init; }
}

// ---------------- Documents & files ----------------

public sealed class DocumentDto
{
    public string EntityType { get; init; } = string.Empty;
    public long DocumentId { get; init; }
    public long EntityId { get; init; }
    public int DocumentTypeId { get; init; }
    public string DocumentTypeCode { get; init; } = string.Empty;
    public string DocumentTypeName { get; init; } = string.Empty;
    public long StoredFileId { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    [JsonIgnore]
    public string StorageKey { get; init; } = string.Empty;

    public string? DocumentNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public int VerificationStatusId { get; init; }
    public string? Remarks { get; init; }
    public DateTime? VerifiedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}

public sealed class StoredFileDto
{
    public long StoredFileId { get; init; }
    public string StorageKey { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public long UploadedBy { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
