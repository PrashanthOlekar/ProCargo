using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProCargo.API.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.API.Controllers;

/// <summary>
/// Customers. Staff need ViewCustomers / ManageCustomers; a customer can only reach their own profile
/// (the service answers 404 for anybody else's id).
/// </summary>
[Route("api/v1/customers")]
[Authorize]
public sealed class CustomersController : ApiControllerBase
{
    private readonly ICustomerService _customers;

    public CustomersController(ICustomerService customers)
    {
        _customers = customers;
    }

    [HttpGet]
    [HasPermission(Permissions.ViewCustomers)]
    public Task<PagedResult<CustomerListItemDto>> Get([FromQuery] CustomerSearchRequest request, CancellationToken cancellationToken) =>
        _customers.GetPagedAsync(request, cancellationToken);

    [HttpGet("me")]
    [Authorize(Policy = Policies.External)]
    public Task<CustomerDto> GetMine(CancellationToken cancellationToken) => _customers.GetMineAsync(cancellationToken);

    [HttpGet("{customerId:long}")]
    public Task<CustomerDto> GetById(long customerId, CancellationToken cancellationToken) => _customers.GetByIdAsync(customerId, cancellationToken);

    [HttpPut("{customerId:long}")]
    public async Task<IActionResult> Update(long customerId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        await _customers.UpdateAsync(customerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{customerId:long}/activate")]
    [HasPermission(Permissions.ManageCustomers)]
    public async Task<IActionResult> Activate(long customerId, CancellationToken cancellationToken)
    {
        await _customers.SetActiveAsync(customerId, true, cancellationToken);
        return NoContent();
    }

    [HttpPost("{customerId:long}/deactivate")]
    [HasPermission(Permissions.ManageCustomers)]
    public async Task<IActionResult> Deactivate(long customerId, CancellationToken cancellationToken)
    {
        await _customers.SetActiveAsync(customerId, false, cancellationToken);
        return NoContent();
    }

    [HttpGet("{customerId:long}/addresses")]
    public Task<IReadOnlyList<CustomerAddressDto>> GetAddresses(long customerId, CancellationToken cancellationToken) =>
        _customers.GetAddressesAsync(customerId, cancellationToken);

    [HttpPost("{customerId:long}/addresses")]
    public async Task<CreatedResponse> AddAddress(long customerId, SaveCustomerAddressRequest request, CancellationToken cancellationToken) =>
        new(await _customers.SaveAddressAsync(customerId, null, request, cancellationToken), null);

    [HttpPut("{customerId:long}/addresses/{addressId:long}")]
    public async Task<IActionResult> UpdateAddress(long customerId, long addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        await _customers.SaveAddressAsync(customerId, addressId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{customerId:long}/addresses/{addressId:long}")]
    public async Task<IActionResult> DeleteAddress(long customerId, long addressId, CancellationToken cancellationToken)
    {
        await _customers.DeleteAddressAsync(customerId, addressId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{customerId:long}/contacts")]
    public Task<IReadOnlyList<CustomerContactDto>> GetContacts(long customerId, CancellationToken cancellationToken) =>
        _customers.GetContactsAsync(customerId, cancellationToken);

    [HttpPost("{customerId:long}/contacts")]
    public async Task<CreatedResponse> AddContact(long customerId, SaveCustomerContactRequest request, CancellationToken cancellationToken) =>
        new(await _customers.SaveContactAsync(customerId, null, request, cancellationToken), null);

    [HttpPut("{customerId:long}/contacts/{contactId:long}")]
    public async Task<IActionResult> UpdateContact(long customerId, long contactId, SaveCustomerContactRequest request, CancellationToken cancellationToken)
    {
        await _customers.SaveContactAsync(customerId, contactId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{customerId:long}/contacts/{contactId:long}")]
    public async Task<IActionResult> DeleteContact(long customerId, long contactId, CancellationToken cancellationToken)
    {
        await _customers.DeleteContactAsync(customerId, contactId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{customerId:long}/documents")]
    public Task<IReadOnlyList<DocumentDto>> GetDocuments(long customerId, [FromServices] IDocumentService documents, CancellationToken cancellationToken) =>
        documents.GetAsync(DocumentEntityType.Customer, customerId, cancellationToken);
}

/// <summary>Vehicle owners (fleet partners).</summary>
[Route("api/v1/owners")]
[Authorize]
public sealed class OwnersController : ApiControllerBase
{
    private readonly IOwnerService _owners;

    public OwnersController(IOwnerService owners)
    {
        _owners = owners;
    }

    [HttpGet]
    [HasPermission(Permissions.ViewOwners)]
    public Task<PagedResult<OwnerListItemDto>> Get([FromQuery] OwnerSearchRequest request, CancellationToken cancellationToken) =>
        _owners.GetPagedAsync(request, cancellationToken);

    [HttpGet("me")]
    [Authorize(Policy = Policies.External)]
    public Task<OwnerProfileResponse> GetMine(CancellationToken cancellationToken) => _owners.GetMineAsync(cancellationToken);

    [HttpGet("{ownerId:long}")]
    public Task<OwnerProfileResponse> GetById(long ownerId, CancellationToken cancellationToken) => _owners.GetByIdAsync(ownerId, cancellationToken);

    [HttpPut("{ownerId:long}")]
    public async Task<IActionResult> Update(long ownerId, UpdateOwnerRequest request, CancellationToken cancellationToken)
    {
        await _owners.UpdateAsync(ownerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{ownerId:long}/business")]
    public async Task<IActionResult> SaveBusiness(long ownerId, SaveOwnerBusinessRequest request, CancellationToken cancellationToken)
    {
        await _owners.SaveBusinessAsync(ownerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ownerId:long}/addresses")]
    public async Task<CreatedResponse> AddAddress(long ownerId, SaveOwnerAddressRequest request, CancellationToken cancellationToken) =>
        new(await _owners.SaveAddressAsync(ownerId, null, request, cancellationToken), null);

    [HttpPut("{ownerId:long}/addresses/{addressId:long}")]
    public async Task<IActionResult> UpdateAddress(long ownerId, long addressId, SaveOwnerAddressRequest request, CancellationToken cancellationToken)
    {
        await _owners.SaveAddressAsync(ownerId, addressId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{ownerId:long}/addresses/{addressId:long}")]
    public async Task<IActionResult> DeleteAddress(long ownerId, long addressId, CancellationToken cancellationToken)
    {
        await _owners.DeleteAddressAsync(ownerId, addressId, cancellationToken);
        return NoContent();
    }

    /// <summary>Adds a payout bank account. The account number is encrypted at rest; only the last four digits are shown.</summary>
    [HttpPost("{ownerId:long}/bank-accounts")]
    public async Task<CreatedResponse> AddBankAccount(long ownerId, CreateBankAccountRequest request, CancellationToken cancellationToken) =>
        new(await _owners.CreateBankAccountAsync(ownerId, request, cancellationToken), null);

    [HttpPost("{ownerId:long}/bank-accounts/{bankAccountId:long}/deactivate")]
    public async Task<IActionResult> DeactivateBankAccount(long ownerId, long bankAccountId, CancellationToken cancellationToken)
    {
        await _owners.DeactivateBankAccountAsync(ownerId, bankAccountId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ownerId:long}/bank-accounts/{bankAccountId:long}/verify")]
    [HasPermission(Permissions.ApproveOwners)]
    public async Task<IActionResult> VerifyBankAccount(long ownerId, long bankAccountId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        await _owners.VerifyBankAccountAsync(ownerId, bankAccountId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Reveals the full account number for a payout (audited, ManageSettlements only).</summary>
    [HttpPost("{ownerId:long}/bank-accounts/{bankAccountId:long}/reveal")]
    [HasPermission(Permissions.ManageSettlements)]
    public Task<BankAccountRevealResponse> RevealBankAccount(long ownerId, long bankAccountId, CancellationToken cancellationToken) =>
        _owners.RevealBankAccountAsync(ownerId, bankAccountId, cancellationToken);

    [HttpPost("{ownerId:long}/verify")]
    [HasPermission(Permissions.ApproveOwners)]
    public async Task<IActionResult> Verify(long ownerId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        await _owners.VerifyAsync(ownerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ownerId:long}/activate")]
    [HasPermission(Permissions.ManageOwners)]
    public async Task<IActionResult> Activate(long ownerId, CancellationToken cancellationToken)
    {
        await _owners.SetActiveAsync(ownerId, true, cancellationToken);
        return NoContent();
    }

    [HttpPost("{ownerId:long}/deactivate")]
    [HasPermission(Permissions.ManageOwners)]
    public async Task<IActionResult> Deactivate(long ownerId, CancellationToken cancellationToken)
    {
        await _owners.SetActiveAsync(ownerId, false, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/drivers")]
[Authorize]
public sealed class DriversController : ApiControllerBase
{
    private readonly IDriverService _drivers;

    public DriversController(IDriverService drivers)
    {
        _drivers = drivers;
    }

    /// <summary>Staff see all drivers; an owner sees the drivers of their fleet.</summary>
    [HttpGet]
    [HasAnyPermission(Permissions.ViewDrivers, Permissions.ManageOwnFleet)]
    public Task<PagedResult<DriverListItemDto>> Get([FromQuery] DriverSearchRequest request, CancellationToken cancellationToken) =>
        _drivers.GetPagedAsync(request, cancellationToken);

    [HttpGet("me")]
    [Authorize(Policy = Policies.External)]
    public Task<DriverDto> GetMine(CancellationToken cancellationToken) => _drivers.GetMineAsync(cancellationToken);

    [HttpGet("available")]
    [HasPermission(Permissions.AssignTrips)]
    public Task<IReadOnlyList<AvailableDriverDto>> GetAvailable([FromQuery] long? ownerId, [FromQuery] DateTime onDateUtc, CancellationToken cancellationToken) =>
        _drivers.GetAvailableForAssignmentAsync(ownerId, onDateUtc, cancellationToken);

    [HttpGet("{driverId:long}")]
    public Task<DriverDto> GetById(long driverId, CancellationToken cancellationToken) => _drivers.GetByIdAsync(driverId, cancellationToken);

    /// <summary>Operations or an owner onboards a driver; the driver receives an invite to set a password.</summary>
    [HttpPost]
    [HasAnyPermission(Permissions.ManageDrivers, Permissions.ManageOwnFleet)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateDriverRequest request, CancellationToken cancellationToken)
    {
        var created = await _drivers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { driverId = created.Id }, created);
    }

    [HttpPut("{driverId:long}")]
    public async Task<IActionResult> Update(long driverId, UpdateDriverRequest request, CancellationToken cancellationToken)
    {
        await _drivers.UpdateAsync(driverId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{driverId:long}/license")]
    public async Task<IActionResult> SetLicense(long driverId, SaveDriverLicenseRequest request, CancellationToken cancellationToken)
    {
        await _drivers.SetLicenseAsync(driverId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{driverId:long}/availability")]
    public async Task<IActionResult> SetAvailability(long driverId, SetDriverAvailabilityRequest request, CancellationToken cancellationToken)
    {
        await _drivers.SetAvailabilityAsync(driverId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{driverId:long}/verify")]
    [HasPermission(Permissions.ApproveDrivers)]
    public async Task<IActionResult> Verify(long driverId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        await _drivers.VerifyAsync(driverId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{driverId:long}/activate")]
    [HasAnyPermission(Permissions.ManageDrivers, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> Activate(long driverId, CancellationToken cancellationToken)
    {
        await _drivers.SetActiveAsync(driverId, true, cancellationToken);
        return NoContent();
    }

    [HttpPost("{driverId:long}/deactivate")]
    [HasAnyPermission(Permissions.ManageDrivers, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> Deactivate(long driverId, CancellationToken cancellationToken)
    {
        await _drivers.SetActiveAsync(driverId, false, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/vehicles")]
[Authorize]
public sealed class VehiclesController : ApiControllerBase
{
    private readonly IVehicleService _vehicles;

    public VehiclesController(IVehicleService vehicles)
    {
        _vehicles = vehicles;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.ViewVehicles, Permissions.ManageOwnFleet)]
    public Task<PagedResult<VehicleListItemDto>> Get([FromQuery] VehicleSearchRequest request, CancellationToken cancellationToken) =>
        _vehicles.GetPagedAsync(request, cancellationToken);

    [HttpGet("available")]
    [HasPermission(Permissions.AssignTrips)]
    public Task<IReadOnlyList<AvailableVehicleDto>> GetAvailable([FromQuery] int vehicleTypeId, [FromQuery] decimal minCapacityKg,
        [FromQuery] DateTime onDateUtc, CancellationToken cancellationToken) =>
        _vehicles.GetAvailableForAssignmentAsync(vehicleTypeId, minCapacityKg, onDateUtc, cancellationToken);

    /// <summary>Vehicles whose insurance / fitness / permit / PUC expire within the given number of days.</summary>
    [HttpGet("expiring")]
    [HasAnyPermission(Permissions.ViewVehicles, Permissions.ManageOwnFleet)]
    public Task<IReadOnlyList<ExpiringVehicleDto>> GetExpiring([FromQuery] int withinDays = 30, CancellationToken cancellationToken = default) =>
        _vehicles.GetExpiringAsync(Math.Clamp(withinDays, 1, 365), cancellationToken);

    [HttpGet("{vehicleId:long}")]
    public Task<VehicleDto> GetById(long vehicleId, CancellationToken cancellationToken) => _vehicles.GetByIdAsync(vehicleId, cancellationToken);

    [HttpPost]
    [HasAnyPermission(Permissions.ManageVehicles, Permissions.ManageOwnFleet)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var id = await _vehicles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { vehicleId = id }, new CreatedResponse(id, null));
    }

    [HttpPut("{vehicleId:long}")]
    [HasAnyPermission(Permissions.ManageVehicles, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> Update(long vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        await _vehicles.UpdateAsync(vehicleId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{vehicleId:long}/availability")]
    [HasAnyPermission(Permissions.ManageVehicles, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> SetAvailability(long vehicleId, SetVehicleAvailabilityRequest request, CancellationToken cancellationToken)
    {
        await _vehicles.SetAvailabilityAsync(vehicleId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{vehicleId:long}/verify")]
    [HasPermission(Permissions.ApproveVehicles)]
    public async Task<IActionResult> Verify(long vehicleId, VerificationDecisionRequest request, CancellationToken cancellationToken)
    {
        await _vehicles.VerifyAsync(vehicleId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{vehicleId:long}/activate")]
    [HasAnyPermission(Permissions.ManageVehicles, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> Activate(long vehicleId, CancellationToken cancellationToken)
    {
        await _vehicles.SetActiveAsync(vehicleId, true, cancellationToken);
        return NoContent();
    }

    [HttpPost("{vehicleId:long}/deactivate")]
    [HasAnyPermission(Permissions.ManageVehicles, Permissions.ManageOwnFleet)]
    public async Task<IActionResult> Deactivate(long vehicleId, CancellationToken cancellationToken)
    {
        await _vehicles.SetActiveAsync(vehicleId, false, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// KYC / compliance documents for customers, owners, drivers and vehicles:
/// /api/v1/documents/{entityType}/{entityId}. Uploads are validated (size, type, magic number) and stored privately.
/// </summary>
[Route("api/v1/documents/{entityType}/{entityId:long}")]
[Authorize]
public sealed class DocumentsController : ApiControllerBase
{
    private readonly IDocumentService _documents;

    public DocumentsController(IDocumentService documents)
    {
        _documents = documents;
    }

    [HttpGet]
    public Task<IReadOnlyList<DocumentDto>> Get(DocumentEntityType entityType, long entityId, CancellationToken cancellationToken) =>
        _documents.GetAsync(entityType, entityId, cancellationToken);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<CreatedResponse> Upload(DocumentEntityType entityType, long entityId, [FromForm] UploadDocumentRequest request,
        IFormFile file, CancellationToken cancellationToken) =>
        new(await _documents.UploadAsync(entityType, entityId, request, ToUpload(file), cancellationToken), null);

    [HttpGet("{documentId:long}/file")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Download(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken) =>
        ToFileResult(await _documents.DownloadAsync(entityType, entityId, documentId, cancellationToken));

    [HttpPost("{documentId:long}/verify")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<IActionResult> Verify(DocumentEntityType entityType, long entityId, long documentId, VerificationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await _documents.VerifyAsync(entityType, entityId, documentId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{documentId:long}")]
    public async Task<IActionResult> Delete(DocumentEntityType entityType, long entityId, long documentId, CancellationToken cancellationToken)
    {
        await _documents.DeleteAsync(entityType, entityId, documentId, cancellationToken);
        return NoContent();
    }
}
