using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Interfaces.Repositories;

public interface IMasterDataRepository
{
    Task<IReadOnlyList<LookupItemDto>> GetLookupsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StateDto>> GetStatesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveStateAsync(int? stateId, SaveStateRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CityDto>> GetCitiesAsync(int? stateId, bool includeInactive, CancellationToken cancellationToken);
    Task<CityDto?> GetCityAsync(int cityId, CancellationToken cancellationToken);
    Task<int> SaveCityAsync(int? cityId, SaveCityRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveVehicleTypeAsync(int? vehicleTypeId, SaveVehicleTypeRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoodsTypeDto>> GetGoodsTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveGoodsTypeAsync(int? goodsTypeId, SaveGoodsTypeRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? appliesTo, bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveDocumentTypeAsync(int? documentTypeId, SaveDocumentTypeRequest request, long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SystemSettingDto>> GetSystemSettingsAsync(CancellationToken cancellationToken);
    Task UpdateSystemSettingAsync(string key, string value, long userId, CancellationToken cancellationToken);
}
