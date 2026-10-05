using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class DriverRepository : IDriverRepository
{
    private readonly StoredProcedureExecutor _sp;

    public DriverRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<DriverListItemDto>> GetPagedAsync(DriverSearchRequest request, long? ownerScope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "Name", "LicenseExpiry");
        var ownerId = ownerScope ?? request.OwnerId;
        var rows = await _sp.QueryAsync<DriverListItemDto>($"""
            EXEC core.usp_Driver_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @OwnerId={ownerId},
                @VerificationStatusId={(int?)request.VerificationStatus}, @AvailabilityStatusId={(int?)request.AvailabilityStatus},
                @IsActive={request.IsActive}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<DriverDto?> GetByIdAsync(long driverId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<DriverDto>($"EXEC core.usp_Driver_GetById @DriverId={driverId}, @UserId={(long?)null}", cancellationToken);

    public Task<DriverDto?> GetByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<DriverDto>($"EXEC core.usp_Driver_GetById @DriverId={(long?)null}, @UserId={userId}", cancellationToken);

    public Task<RegistrationResult> CreateAsync(CreateDriverCommand c, CancellationToken cancellationToken) =>
        _sp.QuerySingleAsync<RegistrationResult>($"""
            EXEC core.usp_Driver_CreateWithUser @OwnerId={c.OwnerId}, @FullName={c.FullName}, @Email={c.Email}, @NormalizedEmail={c.NormalizedEmail},
                @PhoneNumber={c.PhoneNumber}, @AlternatePhoneNumber={c.AlternatePhoneNumber}, @DateOfBirth={c.DateOfBirth},
                @PasswordHash={c.PasswordHash}, @LicenseNumber={c.LicenseNumber}, @LicenseClass={c.LicenseClass},
                @LicenseIssueDate={c.LicenseIssueDate}, @LicenseExpiryDate={c.LicenseExpiryDate}, @IssuingAuthority={c.IssuingAuthority},
                @CreatedBy={c.CreatedBy}
            """, cancellationToken);

    public Task UpdateAsync(long driverId, UpdateDriverRequest r, string normalizedPhone, string? normalizedAltPhone, long modifiedBy,
        CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Driver_Update @DriverId={driverId}, @FullName={r.FullName.Trim()}, @PhoneNumber={normalizedPhone},
                @AlternatePhoneNumber={normalizedAltPhone}, @DateOfBirth={r.DateOfBirth}, @ModifiedBy={modifiedBy}, @RowVersion={r.RowVersion}
            """, cancellationToken);

    public Task SetLicenseAsync(long driverId, SaveDriverLicenseRequest r, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Driver_SetLicense @DriverId={driverId}, @LicenseNumber={r.LicenseNumber}, @LicenseClass={r.LicenseClass.Trim()},
                @IssueDate={r.IssueDate}, @ExpiryDate={r.ExpiryDate}, @IssuingAuthority={r.IssuingAuthority?.Trim()}, @UserId={userId}
            """, cancellationToken);

    public Task SetAvailabilityAsync(long driverId, DriverAvailabilityStatus status, string? reason, long changedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Driver_SetAvailability @DriverId={driverId}, @AvailabilityStatusId={(int)status}, @Reason={reason}, @ChangedBy={changedBy}
            """, cancellationToken);

    public Task SetVerificationAsync(long driverId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Driver_SetVerification @DriverId={driverId}, @VerificationStatusId={(int)status}, @Remarks={remarks}, @VerifiedBy={verifiedBy}
            """, cancellationToken);

    public Task SetActiveAsync(long driverId, bool isActive, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Driver_SetActive @DriverId={driverId}, @IsActive={isActive}, @ModifiedBy={modifiedBy}", cancellationToken);

    public Task<IReadOnlyList<AvailableDriverDto>> GetAvailableForAssignmentAsync(long? ownerId, DateTime onDateUtc, CancellationToken cancellationToken) =>
        _sp.QueryAsync<AvailableDriverDto>(
            $"EXEC core.usp_Driver_GetAvailableForAssignment @OwnerId={ownerId}, @OnDateUtc={onDateUtc}", cancellationToken);
}
