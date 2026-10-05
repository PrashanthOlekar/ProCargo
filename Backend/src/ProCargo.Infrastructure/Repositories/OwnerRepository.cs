using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class OwnerRepository : IOwnerRepository
{
    private readonly StoredProcedureExecutor _sp;

    public OwnerRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<OwnerListItemDto>> GetPagedAsync(OwnerSearchRequest request, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "Name");
        var rows = await _sp.QueryAsync<OwnerListItemDto>($"""
            EXEC core.usp_Owner_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search},
                @VerificationStatusId={(int?)request.VerificationStatus}, @IsActive={request.IsActive}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<OwnerDto?> GetByIdAsync(long ownerId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<OwnerDto>($"EXEC core.usp_Owner_GetById @OwnerId={ownerId}, @UserId={(long?)null}", cancellationToken);

    public Task<OwnerDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<OwnerDto>($"EXEC core.usp_Owner_GetById @OwnerId={(long?)null}, @UserId={userId}", cancellationToken);

    public Task UpdateAsync(UpdateOwnerCommand c, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Owner_Update @OwnerId={c.OwnerId}, @OwnerTypeId={(int)c.OwnerType}, @FullName={c.FullName}, @PhoneNumber={c.PhoneNumber},
                @UpdatePan={c.UpdatePan}, @PanNumberEncrypted={c.PanEncrypted}, @PanLast4={c.PanLast4}, @ModifiedBy={c.ModifiedBy},
                @RowVersion={c.RowVersion}
            """, cancellationToken);

    public Task SaveBusinessAsync(long ownerId, SaveOwnerBusinessRequest r, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_OwnerBusiness_Save @OwnerId={ownerId}, @BusinessName={r.BusinessName.Trim()}, @GstNumber={r.GstNumber?.Trim().ToUpperInvariant()},
                @RegistrationNumber={r.RegistrationNumber?.Trim()}, @FleetSize={r.FleetSize}, @UserId={userId}
            """, cancellationToken);

    public Task<IReadOnlyList<OwnerAddressDto>> GetAddressesAsync(long ownerId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<OwnerAddressDto>($"EXEC core.usp_OwnerAddress_GetByOwner @OwnerId={ownerId}", cancellationToken);

    public async Task<long> SaveAddressAsync(long ownerId, long? addressId, SaveOwnerAddressRequest r, long userId, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_OwnerAddress_Save @OwnerAddressId={addressId}, @OwnerId={ownerId}, @AddressLine1={r.AddressLine1.Trim()},
                @AddressLine2={r.AddressLine2?.Trim()}, @Landmark={r.Landmark?.Trim()}, @CityId={r.CityId}, @Pincode={r.Pincode},
                @IsPrimary={r.IsPrimary}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task DeleteAddressAsync(long ownerId, long addressId, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_OwnerAddress_Delete @OwnerAddressId={addressId}, @OwnerId={ownerId}, @UserId={userId}", cancellationToken);

    public Task<IReadOnlyList<OwnerBankAccountDto>> GetBankAccountsAsync(long ownerId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<OwnerBankAccountDto>($"EXEC core.usp_OwnerBankAccount_GetByOwner @OwnerId={ownerId}", cancellationToken);

    public Task<OwnerBankAccountSensitiveRow?> GetBankAccountSensitiveAsync(long ownerId, long bankAccountId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<OwnerBankAccountSensitiveRow>(
            $"EXEC core.usp_OwnerBankAccount_GetSensitive @OwnerBankAccountId={bankAccountId}, @OwnerId={ownerId}", cancellationToken);

    public async Task<long> CreateBankAccountAsync(CreateBankAccountCommand c, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_OwnerBankAccount_Create @OwnerId={c.OwnerId}, @AccountHolderName={c.AccountHolderName}, @BankName={c.BankName},
                @AccountNumberEncrypted={c.AccountNumberEncrypted}, @AccountNumberLast4={c.AccountNumberLast4}, @IfscCode={c.IfscCode},
                @IsPrimary={c.IsPrimary}, @CreatedBy={c.CreatedBy}
            """, cancellationToken)).Id;

    public Task DeactivateBankAccountAsync(long ownerId, long bankAccountId, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_OwnerBankAccount_Deactivate @OwnerBankAccountId={bankAccountId}, @OwnerId={ownerId}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task SetBankAccountVerificationAsync(long ownerId, long bankAccountId, VerificationStatus status, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_OwnerBankAccount_SetVerification @OwnerBankAccountId={bankAccountId}, @OwnerId={ownerId},
                @VerificationStatusId={(int)status}, @ModifiedBy={modifiedBy}
            """, cancellationToken);

    public Task SetVerificationAsync(long ownerId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Owner_SetVerification @OwnerId={ownerId}, @VerificationStatusId={(int)status}, @Remarks={remarks}, @VerifiedBy={verifiedBy}
            """, cancellationToken);

    public Task SetActiveAsync(long ownerId, bool isActive, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Owner_SetActive @OwnerId={ownerId}, @IsActive={isActive}, @ModifiedBy={modifiedBy}", cancellationToken);
}
