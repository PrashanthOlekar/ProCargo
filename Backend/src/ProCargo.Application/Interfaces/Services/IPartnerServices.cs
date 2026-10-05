using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetPagedAsync(CustomerSearchRequest request, CancellationToken cancellationToken);
    Task<CustomerDto> GetByIdAsync(long customerId, CancellationToken cancellationToken);
    Task<CustomerDto> GetMineAsync(CancellationToken cancellationToken);
    Task UpdateAsync(long customerId, UpdateCustomerRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(long customerId, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerAddressDto>> GetAddressesAsync(long customerId, CancellationToken cancellationToken);
    Task<long> SaveAddressAsync(long customerId, long? addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken);
    Task DeleteAddressAsync(long customerId, long addressId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerContactDto>> GetContactsAsync(long customerId, CancellationToken cancellationToken);
    Task<long> SaveContactAsync(long customerId, long? contactId, SaveCustomerContactRequest request, CancellationToken cancellationToken);
    Task DeleteContactAsync(long customerId, long contactId, CancellationToken cancellationToken);
}

public interface IOwnerService
{
    Task<PagedResult<OwnerListItemDto>> GetPagedAsync(OwnerSearchRequest request, CancellationToken cancellationToken);
    Task<OwnerProfileResponse> GetByIdAsync(long ownerId, CancellationToken cancellationToken);
    Task<OwnerProfileResponse> GetMineAsync(CancellationToken cancellationToken);
    Task UpdateAsync(long ownerId, UpdateOwnerRequest request, CancellationToken cancellationToken);
    Task SaveBusinessAsync(long ownerId, SaveOwnerBusinessRequest request, CancellationToken cancellationToken);
    Task<long> SaveAddressAsync(long ownerId, long? addressId, SaveOwnerAddressRequest request, CancellationToken cancellationToken);
    Task DeleteAddressAsync(long ownerId, long addressId, CancellationToken cancellationToken);
    Task<long> CreateBankAccountAsync(long ownerId, CreateBankAccountRequest request, CancellationToken cancellationToken);
    Task DeactivateBankAccountAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken);
    Task VerifyBankAccountAsync(long ownerId, long bankAccountId, VerificationDecisionRequest request, CancellationToken cancellationToken);
    Task<BankAccountRevealResponse> RevealBankAccountAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken);
    Task VerifyAsync(long ownerId, VerificationDecisionRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(long ownerId, bool isActive, CancellationToken cancellationToken);
}

public interface IDriverService
{
    Task<PagedResult<DriverListItemDto>> GetPagedAsync(DriverSearchRequest request, CancellationToken cancellationToken);
    Task<DriverDto> GetByIdAsync(long driverId, CancellationToken cancellationToken);
    Task<DriverDto> GetMineAsync(CancellationToken cancellationToken);
    Task<CreatedResponse> CreateAsync(CreateDriverRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long driverId, UpdateDriverRequest request, CancellationToken cancellationToken);
    Task SetLicenseAsync(long driverId, SaveDriverLicenseRequest request, CancellationToken cancellationToken);
    Task SetAvailabilityAsync(long driverId, SetDriverAvailabilityRequest request, CancellationToken cancellationToken);
    Task VerifyAsync(long driverId, VerificationDecisionRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(long driverId, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableDriverDto>> GetAvailableForAssignmentAsync(long? ownerId, DateTime onDateUtc, CancellationToken cancellationToken);
}

public interface IVehicleService
{
    Task<PagedResult<VehicleListItemDto>> GetPagedAsync(VehicleSearchRequest request, CancellationToken cancellationToken);
    Task<VehicleDto> GetByIdAsync(long vehicleId, CancellationToken cancellationToken);
    Task<long> CreateAsync(CreateVehicleRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken);
    Task SetAvailabilityAsync(long vehicleId, SetVehicleAvailabilityRequest request, CancellationToken cancellationToken);
    Task VerifyAsync(long vehicleId, VerificationDecisionRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(long vehicleId, bool isActive, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableVehicleDto>> GetAvailableForAssignmentAsync(int vehicleTypeId, decimal minCapacityKg, DateTime onDateUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExpiringVehicleDto>> GetExpiringAsync(int withinDays, CancellationToken cancellationToken);
}

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetAsync(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken);
    Task<long> UploadAsync(DocumentEntityType entityType, long entityId, UploadDocumentRequest request, FileUpload file, CancellationToken cancellationToken);
    Task<FileDownload> DownloadAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken);
    Task VerifyAsync(DocumentEntityType entityType, long entityId, long documentId, VerificationDecisionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken);
}

/// <summary>Validates uploads (extension, MIME type, size, file signature) and stores them safely.</summary>
public interface IFileService
{
    Task<long> StoreAsync(FileUpload file, string category, CancellationToken cancellationToken);
    Task<FileDownload> OpenAsync(long storedFileId, CancellationToken cancellationToken);
}
