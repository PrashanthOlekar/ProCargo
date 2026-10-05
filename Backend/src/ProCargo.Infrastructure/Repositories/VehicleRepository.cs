using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class VehicleRepository : IVehicleRepository
{
    private readonly StoredProcedureExecutor _sp;

    public VehicleRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public async Task<PagedResult<VehicleListItemDto>> GetPagedAsync(VehicleSearchRequest request, long? ownerScope, CancellationToken cancellationToken)
    {
        var (page, size, search, sort, direction) = request.Normalize("CreatedDate", "CreatedDate", "VehicleNumber", "InsuranceExpiry");
        var ownerId = ownerScope ?? request.OwnerId;
        var rows = await _sp.QueryAsync<VehicleListItemDto>($"""
            EXEC core.usp_Vehicle_GetPaged @PageNumber={page}, @PageSize={size}, @Search={search}, @OwnerId={ownerId},
                @VehicleTypeId={request.VehicleTypeId}, @VerificationStatusId={(int?)request.VerificationStatus}, @IsAvailable={request.IsAvailable},
                @IsActive={request.IsActive}, @SortBy={sort}, @SortDirection={direction}
            """, cancellationToken);
        return rows.ToPagedResult(page, size);
    }

    public Task<VehicleDto?> GetByIdAsync(long vehicleId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<VehicleDto>($"EXEC core.usp_Vehicle_GetById @VehicleId={vehicleId}", cancellationToken);

    public async Task<long> CreateAsync(SaveVehicleCommand c, CancellationToken cancellationToken) =>
        (await _sp.QuerySingleAsync<IdResult>($"""
            EXEC core.usp_Vehicle_Create @OwnerId={c.OwnerId}, @VehicleNumber={c.VehicleNumber}, @VehicleTypeId={c.VehicleTypeId},
                @Manufacturer={c.Manufacturer}, @Model={c.Model}, @ManufactureYear={c.ManufactureYear}, @CapacityKg={c.CapacityKg},
                @PermitNumber={c.PermitNumber}, @PermitExpiryDate={c.PermitExpiryDate}, @InsuranceNumber={c.InsuranceNumber},
                @InsuranceExpiryDate={c.InsuranceExpiryDate}, @FitnessExpiryDate={c.FitnessExpiryDate}, @PucExpiryDate={c.PucExpiryDate},
                @CreatedBy={c.UserId}
            """, cancellationToken)).Id;

    public Task UpdateAsync(SaveVehicleCommand c, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Vehicle_Update @VehicleId={c.VehicleId}, @VehicleTypeId={c.VehicleTypeId}, @Manufacturer={c.Manufacturer},
                @Model={c.Model}, @ManufactureYear={c.ManufactureYear}, @CapacityKg={c.CapacityKg}, @PermitNumber={c.PermitNumber},
                @PermitExpiryDate={c.PermitExpiryDate}, @InsuranceNumber={c.InsuranceNumber}, @InsuranceExpiryDate={c.InsuranceExpiryDate},
                @FitnessExpiryDate={c.FitnessExpiryDate}, @PucExpiryDate={c.PucExpiryDate}, @ModifiedBy={c.UserId}, @RowVersion={c.RowVersion}
            """, cancellationToken);

    public Task SetAvailabilityAsync(long vehicleId, bool isAvailable, string? reason, long changedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Vehicle_SetAvailability @VehicleId={vehicleId}, @IsAvailable={isAvailable}, @Reason={reason}, @ChangedBy={changedBy}
            """, cancellationToken);

    public Task SetVerificationAsync(long vehicleId, VerificationStatus status, string? remarks, long verifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"""
            EXEC core.usp_Vehicle_SetVerification @VehicleId={vehicleId}, @VerificationStatusId={(int)status}, @Remarks={remarks}, @VerifiedBy={verifiedBy}
            """, cancellationToken);

    public Task SetActiveAsync(long vehicleId, bool isActive, long modifiedBy, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC core.usp_Vehicle_SetActive @VehicleId={vehicleId}, @IsActive={isActive}, @ModifiedBy={modifiedBy}", cancellationToken);

    public Task<IReadOnlyList<AvailableVehicleDto>> GetAvailableForAssignmentAsync(int vehicleTypeId, decimal minCapacityKg, DateTime onDateUtc,
        CancellationToken cancellationToken) =>
        _sp.QueryAsync<AvailableVehicleDto>($"""
            EXEC core.usp_Vehicle_GetAvailableForAssignment @VehicleTypeId={vehicleTypeId}, @MinCapacityKg={minCapacityKg}, @OnDateUtc={onDateUtc}
            """, cancellationToken);

    public Task<IReadOnlyList<ExpiringVehicleDto>> GetExpiringAsync(int withinDays, long? ownerId, CancellationToken cancellationToken) =>
        _sp.QueryAsync<ExpiringVehicleDto>($"EXEC core.usp_Vehicle_GetExpiring @WithinDays={withinDays}, @OwnerId={ownerId}", cancellationToken);
}
