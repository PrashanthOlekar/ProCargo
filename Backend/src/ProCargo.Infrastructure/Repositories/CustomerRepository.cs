using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class CustomerRepository : ICustomerRepository
{
    private readonly StoredProcedureExecutor _sp;

    public CustomerRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<CustomerListItemDto>> GetPagedAsync(CustomerSearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "Name", "Number");
        var rows = await _sp.QueryAsync<CustomerListItemDto>($"""
            EXEC core.usp_Customer_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search},
                @CustomerTypeId={(int?)request.CustomerType}, @IsActive={request.IsActive}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<CustomerDto?> GetByIdAsync(long customerId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<CustomerDto>($"EXEC core.usp_Customer_GetById @CustomerId={customerId}, @UserId={(long?)null}", cancellationToken);

    public Task<CustomerDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<CustomerDto>($"EXEC core.usp_Customer_GetById @CustomerId={(long?)null}, @UserId={userId}", cancellationToken);

    public Task UpdateAsync(long customerId, UpdateCustomerRequest r, string normalizedPhone, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Customer_Update @CustomerId={customerId}, @CustomerTypeId={(int)r.CustomerType}, @FullName={r.FullName.Trim()},
                @CompanyName={r.CompanyName?.Trim()}, @PhoneNumber={normalizedPhone}, @GstNumber={r.GstNumber?.Trim().ToUpperInvariant()},
                @ModifiedBy={modifiedBy}, @RowVersion={r.RowVersion}
            """, cancellationToken);

    public Task SetActiveAsync(long customerId, bool isActive, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Customer_SetActive @CustomerId={customerId}, @IsActive={isActive}, @ModifiedBy={modifiedBy}", cancellationToken);

    public Task<IReadOnlyList<CustomerAddressDto>> GetAddressesAsync(long customerId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<CustomerAddressDto>($"EXEC core.usp_CustomerAddress_GetByCustomer @CustomerId={customerId}", cancellationToken);

    public async Task<long> SaveAddressAsync(long customerId, long? addressId, SaveCustomerAddressRequest r, long userId, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_CustomerAddress_Save @CustomerAddressId={addressId}, @CustomerId={customerId}, @Label={r.Label.Trim()},
                @AddressLine1={r.AddressLine1.Trim()}, @AddressLine2={r.AddressLine2?.Trim()}, @Landmark={r.Landmark?.Trim()}, @CityId={r.CityId},
                @Pincode={r.Pincode}, @Latitude={SqlParams.Coordinate("Latitude", r.Latitude)}, @Longitude={SqlParams.Coordinate("Longitude", r.Longitude)},
                @IsDefault={r.IsDefault}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task DeleteAddressAsync(long customerId, long addressId, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_CustomerAddress_Delete @CustomerAddressId={addressId}, @CustomerId={customerId}, @UserId={userId}
            """, cancellationToken);

    public Task<IReadOnlyList<CustomerContactDto>> GetContactsAsync(long customerId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<CustomerContactDto>($"EXEC core.usp_CustomerContact_GetByCustomer @CustomerId={customerId}", cancellationToken);

    public async Task<long> SaveContactAsync(long customerId, long? contactId, SaveCustomerContactRequest r, string normalizedPhone, long userId,
        CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_CustomerContact_Save @CustomerContactId={contactId}, @CustomerId={customerId}, @ContactName={r.ContactName.Trim()},
                @PhoneNumber={normalizedPhone}, @Email={r.Email?.Trim()}, @Designation={r.Designation?.Trim()}, @IsPrimary={r.IsPrimary}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task DeleteContactAsync(long customerId, long contactId, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_CustomerContact_Delete @CustomerContactId={contactId}, @CustomerId={customerId}, @UserId={userId}
            """, cancellationToken);
}
