using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<PagedResult<CustomerListItemDto>> GetPagedAsync(CustomerSearchRequest request, CancellationToken cancellationToken);
    Task<CustomerDto?> GetByIdAsync(long customerId, CancellationToken cancellationToken);
    Task<CustomerDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task UpdateAsync(long customerId, UpdateCustomerRequest request, string normalizedPhone, long modifiedBy, CancellationToken cancellationToken);
    Task SetActiveAsync(long customerId, bool isActive, long modifiedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAddressDto>> GetAddressesAsync(long customerId, CancellationToken cancellationToken);
    Task<long> SaveAddressAsync(long customerId, long? addressId, SaveCustomerAddressRequest request, long userId, CancellationToken cancellationToken);
    Task DeleteAddressAsync(long customerId, long addressId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerContactDto>> GetContactsAsync(long customerId, CancellationToken cancellationToken);
    Task<long> SaveContactAsync(long customerId, long? contactId, SaveCustomerContactRequest request, string normalizedPhone, long userId, CancellationToken cancellationToken);
    Task DeleteContactAsync(long customerId, long contactId, long userId, CancellationToken cancellationToken);
}

public sealed record UpdateOwnerCommand(
    long OwnerId,
    OwnerType OwnerType,
    string FullName,
    string PhoneNumber,
    bool UpdatePan,
    string? PanEncrypted,
    string? PanLast4,
    byte[] RowVersion,
    long ModifiedBy);

public sealed record CreateBankAccountCommand(
    long OwnerId,
    string AccountHolderName,
    string BankName,
    string AccountNumberEncrypted,
    string AccountNumberLast4,
    string IfscCode,
    bool IsPrimary,
    long CreatedBy);

public interface IOwnerRepository
{
    Task<PagedResult<OwnerListItemDto>> GetPagedAsync(OwnerSearchRequest request, CancellationToken cancellationToken);
    Task<OwnerDto?> GetByIdAsync(long ownerId, CancellationToken cancellationToken);
    Task<OwnerDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task UpdateAsync(UpdateOwnerCommand command, CancellationToken cancellationToken);
    Task SaveBusinessAsync(long ownerId, SaveOwnerBusinessRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OwnerAddressDto>> GetAddressesAsync(long ownerId, CancellationToken cancellationToken);
    Task<long> SaveAddressAsync(long ownerId, long? addressId, SaveOwnerAddressRequest request, long userId, CancellationToken cancellationToken);
    Task DeleteAddressAsync(long ownerId, long addressId, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OwnerBankAccountDto>> GetBankAccountsAsync(long ownerId, CancellationToken cancellationToken);
    Task<OwnerBankAccountSensitiveRow?> GetBankAccountSensitiveAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken);
    Task<long> CreateBankAccountAsync(CreateBankAccountCommand command, CancellationToken cancellationToken);
    Task DeactivateBankAccountAsync(long ownerId, long bankAccountId, long modifiedBy, CancellationToken cancellationToken);
    Task SetBankAccountVerificationAsync(long ownerId, long bankAccountId, VerificationStatus status, long modifiedBy, CancellationToken cancellationToken);
    Task SetVerificationAsync(long ownerId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken);
    Task SetActiveAsync(long ownerId, bool isActive, long modifiedBy, CancellationToken cancellationToken);
}

public sealed record CreateDriverCommand(
    long? OwnerId,
    string FullName,
    string Email,
    string NormalizedEmail,
    string PhoneNumber,
    string? AlternatePhoneNumber,
    DateOnly? DateOfBirth,
    string PasswordHash,
    string LicenseNumber,
    string LicenseClass,
    DateOnly? LicenseIssueDate,
    DateOnly LicenseExpiryDate,
    string? IssuingAuthority,
    long CreatedBy);

public interface IDriverRepository
{
    Task<PagedResult<DriverListItemDto>> GetPagedAsync(DriverSearchRequest request, long? ownerScope, CancellationToken cancellationToken);
    Task<DriverDto?> GetByIdAsync(long driverId, CancellationToken cancellationToken);
    Task<DriverDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<RegistrationResult> CreateAsync(CreateDriverCommand command, CancellationToken cancellationToken);
    Task UpdateAsync(long driverId, UpdateDriverRequest request, string normalizedPhone, string? normalizedAltPhone, long modifiedBy, CancellationToken cancellationToken);
    Task SetLicenseAsync(long driverId, SaveDriverLicenseRequest request, long userId, CancellationToken cancellationToken);
    Task SetAvailabilityAsync(long driverId, DriverAvailabilityStatus status, string? reason, long changedBy, CancellationToken cancellationToken);
    Task SetVerificationAsync(long driverId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken);
    Task SetActiveAsync(long driverId, bool isActive, long modifiedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableDriverDto>> GetAvailableForAssignmentAsync(long? ownerId, DateTime onDateUtc, CancellationToken cancellationToken);
}

public sealed record SaveVehicleCommand(
    long? VehicleId,
    long OwnerId,
    string? VehicleNumber,
    int VehicleTypeId,
    string Manufacturer,
    string Model,
    int? ManufactureYear,
    decimal CapacityKg,
    string? PermitNumber,
    DateOnly? PermitExpiryDate,
    string? InsuranceNumber,
    DateOnly? InsuranceExpiryDate,
    DateOnly? FitnessExpiryDate,
    DateOnly? PucExpiryDate,
    byte[]? RowVersion,
    long UserId);

public interface IVehicleRepository
{
    Task<PagedResult<VehicleListItemDto>> GetPagedAsync(VehicleSearchRequest request, long? ownerScope, CancellationToken cancellationToken);
    Task<VehicleDto?> GetByIdAsync(long vehicleId, CancellationToken cancellationToken);
    Task<long> CreateAsync(SaveVehicleCommand command, CancellationToken cancellationToken);
    Task UpdateAsync(SaveVehicleCommand command, CancellationToken cancellationToken);
    Task SetAvailabilityAsync(long vehicleId, bool isAvailable, string? reason, long changedBy, CancellationToken cancellationToken);
    Task SetVerificationAsync(long vehicleId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken);
    Task SetActiveAsync(long vehicleId, bool isActive, long modifiedBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableVehicleDto>> GetAvailableForAssignmentAsync(int vehicleTypeId, decimal minCapacityKg, DateTime onDateUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExpiringVehicleDto>> GetExpiringAsync(int withinDays, long? ownerId, CancellationToken cancellationToken);
}

public sealed record StoredFileCommand(string StorageKey, string OriginalFileName, string ContentType, long SizeBytes, byte[] Sha256Hash, long UploadedBy);

public interface IDocumentRepository
{
    Task<long> CreateStoredFileAsync(StoredFileCommand command, CancellationToken cancellationToken);
    Task<StoredFileDto?> GetStoredFileAsync(long storedFileId, CancellationToken cancellationToken);
    Task<long> CreateDocumentAsync(DocumentEntityType entityType, long entityId, int documentTypeId, long storedFileId,
        string? documentNumber, DateOnly? expiryDate, long createdBy, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken);
    Task<DocumentDto?> GetDocumentAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken);
    Task SetVerificationAsync(DocumentEntityType entityType, long entityId, long documentId, VerificationStatus status, string? remarks,
        long verifiedBy, CancellationToken cancellationToken);
    Task DeleteAsync(DocumentEntityType entityType, long entityId, long documentId, long modifiedBy, CancellationToken cancellationToken);
}
