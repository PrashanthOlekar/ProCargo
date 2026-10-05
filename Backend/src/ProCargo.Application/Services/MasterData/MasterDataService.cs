using Microsoft.Extensions.Caching.Memory;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Services.MasterData;

/// <summary>
/// Reference data with a short in-memory cache (it is read on every page load and changes rarely).
/// Every write invalidates the cache on this instance; other instances refresh within the cache window.
/// </summary>
public sealed class MasterDataService : IMasterDataService
{
    private const string ReferenceCacheKey = "procargo:reference-data";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly IMasterDataRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ISettingsProvider _settings;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public MasterDataService(IMasterDataRepository repository, IMemoryCache cache, ISettingsProvider settings,
        ICurrentUser currentUser, IAuditLogger audit)
    {
        _repository = repository;
        _cache = cache;
        _settings = settings;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<ReferenceDataResponse> GetReferenceDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ReferenceCacheKey, out ReferenceDataResponse? cached) && cached is not null)
        {
            return cached;
        }

        var lookups = await _repository.GetLookupsAsync(cancellationToken);
        var vehicleTypes = await _repository.GetVehicleTypesAsync(false, cancellationToken);
        var goodsTypes = await _repository.GetGoodsTypesAsync(false, cancellationToken);
        var states = await _repository.GetStatesAsync(false, cancellationToken);

        var grouped = lookups
            .GroupBy(l => l.LookupType)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<LookupItemDto>)g.OrderBy(i => i.SortOrder).ToList());

        var response = new ReferenceDataResponse(grouped, vehicleTypes, goodsTypes, states);
        _cache.Set(ReferenceCacheKey, response, CacheDuration);
        return response;
    }

    public Task<IReadOnlyList<StateDto>> GetStatesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _repository.GetStatesAsync(includeInactive, cancellationToken);

    public Task<int> SaveStateAsync(int? stateId, SaveStateRequest request, CancellationToken cancellationToken) =>
        SaveAsync("State", stateId, request, () => _repository.SaveStateAsync(stateId, request, _currentUser.UserId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<CityDto>> GetCitiesAsync(int? stateId, bool includeInactive, CancellationToken cancellationToken) =>
        _repository.GetCitiesAsync(stateId, includeInactive, cancellationToken);

    public Task<int> SaveCityAsync(int? cityId, SaveCityRequest request, CancellationToken cancellationToken) =>
        SaveAsync("City", cityId, request, () => _repository.SaveCityAsync(cityId, request, _currentUser.UserId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _repository.GetVehicleTypesAsync(includeInactive, cancellationToken);

    public Task<int> SaveVehicleTypeAsync(int? id, SaveVehicleTypeRequest request, CancellationToken cancellationToken) =>
        SaveAsync("VehicleType", id, request, () => _repository.SaveVehicleTypeAsync(id, request, _currentUser.UserId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<GoodsTypeDto>> GetGoodsTypesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        _repository.GetGoodsTypesAsync(includeInactive, cancellationToken);

    public Task<int> SaveGoodsTypeAsync(int? id, SaveGoodsTypeRequest request, CancellationToken cancellationToken) =>
        SaveAsync("GoodsType", id, request, () => _repository.SaveGoodsTypeAsync(id, request, _currentUser.UserId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? appliesTo, bool includeInactive, CancellationToken cancellationToken) =>
        _repository.GetDocumentTypesAsync(appliesTo, includeInactive, cancellationToken);

    public Task<int> SaveDocumentTypeAsync(int? id, SaveDocumentTypeRequest request, CancellationToken cancellationToken) =>
        SaveAsync("DocumentType", id, request, () => _repository.SaveDocumentTypeAsync(id, request, _currentUser.UserId, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<SystemSettingDto>> GetSystemSettingsAsync(CancellationToken cancellationToken) =>
        _repository.GetSystemSettingsAsync(cancellationToken);

    public async Task UpdateSystemSettingAsync(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken)
    {
        var existing = (await _repository.GetSystemSettingsAsync(cancellationToken)).FirstOrDefault(s => s.SettingKey == key)
                       ?? throw new NotFoundException("SETTING_NOT_FOUND", $"Setting '{key}' was not found.");

        await _repository.UpdateSystemSettingAsync(key, request.Value.Trim(), _currentUser.UserId, cancellationToken);
        _settings.Invalidate();
        await _audit.LogAsync("SystemSettingUpdated", "SystemSetting", key, existing.SettingValue, request.Value, cancellationToken: cancellationToken);
    }

    private async Task<int> SaveAsync(string entity, int? id, object request, Func<Task<int>> save, CancellationToken cancellationToken)
    {
        var savedId = await save();
        _cache.Remove(ReferenceCacheKey);
        await _audit.LogAsync(id is null ? $"{entity}Created" : $"{entity}Updated", entity, savedId, newValue: request,
            cancellationToken: cancellationToken);
        return savedId;
    }
}
