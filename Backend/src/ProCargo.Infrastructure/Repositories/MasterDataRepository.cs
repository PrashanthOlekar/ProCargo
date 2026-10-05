using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Requests;
using ProCargo.Infrastructure.Persistence;

namespace ProCargo.Infrastructure.Repositories;

internal sealed class MasterDataRepository : IMasterDataRepository
{
    private readonly StoredProcedureExecutor _sp;

    public MasterDataRepository(StoredProcedureExecutor sp)
    {
        _sp = sp;
    }

    public Task<IReadOnlyList<LookupItemDto>> GetLookupsAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<LookupItemDto>($"EXEC mst.usp_Lookup_GetAll", cancellationToken);

    public Task<IReadOnlyList<StateDto>> GetStatesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _sp.QueryAsync<StateDto>($"EXEC mst.usp_State_GetAll @IncludeInactive={includeInactive}", cancellationToken);

    public async Task<int> SaveStateAsync(int? stateId, SaveStateRequest r, long userId, CancellationToken cancellationToken) =>
        (int)(await _sp.QuerySingleAsync<IdResult>($"""
            EXEC mst.usp_State_Save @StateId={stateId}, @StateCode={r.StateCode.ToUpperInvariant()}, @Name={r.Name.Trim()},
                @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<CityDto>> GetCitiesAsync(int? stateId, bool includeInactive, CancellationToken cancellationToken) =>
        _sp.QueryAsync<CityDto>($"EXEC mst.usp_City_GetByState @StateId={stateId}, @IncludeInactive={includeInactive}", cancellationToken);

    public Task<CityDto?> GetCityAsync(int cityId, CancellationToken cancellationToken) =>
        _sp.QuerySingleOrDefaultAsync<CityDto>($"EXEC mst.usp_City_GetById @CityId={cityId}", cancellationToken);

    public async Task<int> SaveCityAsync(int? cityId, SaveCityRequest r, long userId, CancellationToken cancellationToken) =>
        (int)(await _sp.QuerySingleAsync<IdResult>($"""
            EXEC mst.usp_City_Save @CityId={cityId}, @StateId={r.StateId}, @Name={r.Name.Trim()}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _sp.QueryAsync<VehicleTypeDto>($"EXEC mst.usp_VehicleType_GetAll @IncludeInactive={includeInactive}", cancellationToken);

    public async Task<int> SaveVehicleTypeAsync(int? id, SaveVehicleTypeRequest r, long userId, CancellationToken cancellationToken) =>
        (int)(await _sp.QuerySingleAsync<IdResult>($"""
            EXEC mst.usp_VehicleType_Save @VehicleTypeId={id}, @Code={r.Code}, @Name={r.Name.Trim()}, @Description={r.Description},
                @CapacityKg={r.CapacityKg}, @LengthFt={r.LengthFt}, @SortOrder={r.SortOrder}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<GoodsTypeDto>> GetGoodsTypesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _sp.QueryAsync<GoodsTypeDto>($"EXEC mst.usp_GoodsType_GetAll @IncludeInactive={includeInactive}", cancellationToken);

    public async Task<int> SaveGoodsTypeAsync(int? id, SaveGoodsTypeRequest r, long userId, CancellationToken cancellationToken) =>
        (int)(await _sp.QuerySingleAsync<IdResult>($"""
            EXEC mst.usp_GoodsType_Save @GoodsTypeId={id}, @Code={r.Code}, @Name={r.Name.Trim()},
                @RequiresSpecialHandling={r.RequiresSpecialHandling}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? appliesTo, bool includeInactive, CancellationToken cancellationToken) =>
        _sp.QueryAsync<DocumentTypeDto>($"EXEC mst.usp_DocumentType_GetAll @AppliesTo={appliesTo}, @IncludeInactive={includeInactive}", cancellationToken);

    public async Task<int> SaveDocumentTypeAsync(int? id, SaveDocumentTypeRequest r, long userId, CancellationToken cancellationToken) =>
        (int)(await _sp.QuerySingleAsync<IdResult>($"""
            EXEC mst.usp_DocumentType_Save @DocumentTypeId={id}, @Code={r.Code}, @Name={r.Name.Trim()}, @AppliesTo={r.AppliesTo},
                @RequiresExpiry={r.RequiresExpiry}, @IsMandatory={r.IsMandatory}, @IsActive={r.IsActive}, @UserId={userId}
            """, cancellationToken)).Id;

    public Task<IReadOnlyList<SystemSettingDto>> GetSystemSettingsAsync(CancellationToken cancellationToken) =>
        _sp.QueryAsync<SystemSettingDto>($"EXEC mst.usp_SystemSetting_GetAll", cancellationToken);

    public Task UpdateSystemSettingAsync(string key, string value, long userId, CancellationToken cancellationToken) =>
        _sp.ExecuteAsync($"EXEC mst.usp_SystemSetting_Update @SettingKey={key}, @SettingValue={value}, @ModifiedBy={userId}", cancellationToken);
}
