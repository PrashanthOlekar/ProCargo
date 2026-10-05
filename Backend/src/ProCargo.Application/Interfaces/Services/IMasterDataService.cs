using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Interfaces.Services;

public interface IMasterDataService
{
    Task<ReferenceDataResponse> GetReferenceDataAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StateDto>> GetStatesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveStateAsync(int? stateId, SaveStateRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CityDto>> GetCitiesAsync(int? stateId, bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveCityAsync(int? cityId, SaveCityRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveVehicleTypeAsync(int? id, SaveVehicleTypeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoodsTypeDto>> GetGoodsTypesAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveGoodsTypeAsync(int? id, SaveGoodsTypeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? appliesTo, bool includeInactive, CancellationToken cancellationToken);
    Task<int> SaveDocumentTypeAsync(int? id, SaveDocumentTypeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SystemSettingDto>> GetSystemSettingsAsync(CancellationToken cancellationToken);
    Task UpdateSystemSettingAsync(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken);
}
