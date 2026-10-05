using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Partners;

/// <summary>
/// Customer profile, addresses and contacts.
/// Access: the customer themself; staff with ViewCustomers (read) / ManageCustomers (write).
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IMasterDataRepository _masterData;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;

    public CustomerService(ICustomerRepository customers, IMasterDataRepository masterData, AccessGuard access, IAuditLogger audit)
    {
        _customers = customers;
        _masterData = masterData;
        _access = access;
        _audit = audit;
    }

    public Task<PagedResult<CustomerListItemDto>> GetPagedAsync(CustomerSearchRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewCustomers);
        return _customers.GetPagedAsync(request, cancellationToken);
    }

    public async Task<CustomerDto> GetByIdAsync(long customerId, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ViewCustomers);
        return await _customers.GetByIdAsync(customerId, cancellationToken) ?? throw NotFoundException.For("Customer", customerId);
    }

    public Task<CustomerDto> GetMineAsync(CancellationToken cancellationToken) =>
        GetByIdAsync(_access.RequireCustomerId(), cancellationToken);

    public async Task UpdateAsync(long customerId, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ManageCustomers);
        await _customers.UpdateAsync(customerId, request, IndianFormats.NormalizePhone(request.PhoneNumber), _access.User.UserId, cancellationToken);
        await _audit.LogAsync("CustomerUpdated", "Customer", customerId, newValue: new { request.FullName, request.CompanyName, request.CustomerType },
            cancellationToken: cancellationToken);
    }

    public async Task SetActiveAsync(long customerId, bool isActive, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageCustomers);
        await _customers.SetActiveAsync(customerId, isActive, _access.User.UserId, cancellationToken);
        await _audit.LogAsync(isActive ? "CustomerActivated" : "CustomerDeactivated", "Customer", customerId, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<CustomerAddressDto>> GetAddressesAsync(long customerId, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ViewCustomers);
        return _customers.GetAddressesAsync(customerId, cancellationToken);
    }

    public async Task<long> SaveAddressAsync(long customerId, long? addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ManageCustomers);
        _ = await _masterData.GetCityAsync(request.CityId, cancellationToken)
            ?? throw new BusinessRuleException("CITY_INVALID", "The selected city does not exist.");
        return await _customers.SaveAddressAsync(customerId, addressId, request, _access.User.UserId, cancellationToken);
    }

    public Task DeleteAddressAsync(long customerId, long addressId, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ManageCustomers);
        return _customers.DeleteAddressAsync(customerId, addressId, _access.User.UserId, cancellationToken);
    }

    public Task<IReadOnlyList<CustomerContactDto>> GetContactsAsync(long customerId, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ViewCustomers);
        return _customers.GetContactsAsync(customerId, cancellationToken);
    }

    public Task<long> SaveContactAsync(long customerId, long? contactId, SaveCustomerContactRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ManageCustomers);
        return _customers.SaveContactAsync(customerId, contactId, request, IndianFormats.NormalizePhone(request.PhoneNumber),
            _access.User.UserId, cancellationToken);
    }

    public Task DeleteContactAsync(long customerId, long contactId, CancellationToken cancellationToken)
    {
        _access.EnsureCustomer(customerId, Permissions.ManageCustomers);
        return _customers.DeleteContactAsync(customerId, contactId, _access.User.UserId, cancellationToken);
    }
}
