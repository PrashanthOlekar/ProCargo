using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Infrastructure.Platform;

/// <summary>Business settings from mst.SystemSetting, cached for five minutes per instance.</summary>
internal sealed class SettingsProvider : ISettingsProvider
{
    private const string CacheKey = "procargo:system-settings";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IMasterDataRepository _repository;
    private readonly IMemoryCache _cache;

    public SettingsProvider(IMasterDataRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken) =>
        int.TryParse(await GetRawAsync(key, cancellationToken), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken cancellationToken) =>
        decimal.TryParse(await GetRawAsync(key, cancellationToken), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;

    public async Task<string> GetStringAsync(string key, string defaultValue, CancellationToken cancellationToken) =>
        await GetRawAsync(key, cancellationToken) ?? defaultValue;

    public void Invalidate() => _cache.Remove(CacheKey);

    private async Task<string?> GetRawAsync(string key, CancellationToken cancellationToken)
    {
        var settings = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var rows = await _repository.GetSystemSettingsAsync(cancellationToken);
            return rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);
        });

        return settings is not null && settings.TryGetValue(key, out var value) ? value : null;
    }
}

/// <summary>
/// Writes aud.AuditLog rows with the caller, IP, user agent and trace id. Values are serialised to JSON with
/// secrets masked (any property whose name looks like a password, token, OTP, secret, PAN or account number).
/// Audit failures are logged but never break the business operation that already succeeded.
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private static readonly string[] SensitiveNames = ["password", "token", "otp", "secret", "signature", "pannumber", "accountnumber", "cvv", "hash"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IAuditRepository repository, ICurrentUser currentUser, ILogger<AuditLogger> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, object? entityId, object? oldValue = null, object? newValue = null,
        long? userId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _repository.CreateAsync(new AuditEntry(
                userId ?? _currentUser.UserIdOrNull,
                Truncate(action, 100)!,
                Truncate(entityType, 50)!,
                Truncate(Convert.ToString(entityId, CultureInfo.InvariantCulture), 50),
                Serialize(oldValue),
                Serialize(newValue),
                Truncate(_currentUser.IpAddress, 45),
                Truncate(_currentUser.UserAgent, 300),
                Truncate(_currentUser.TraceId, 64)), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audit entry {Action} for {EntityType} {EntityId} could not be written", action, entityType, entityId);
        }
    }

    internal static string? Serialize(object? value)
    {
        if (value is null) return null;

        var node = JsonSerializer.SerializeToNode(value, JsonOptions);
        Mask(node);
        return Truncate(node?.ToJsonString(JsonOptions), 4000);
    }

    private static void Mask(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    var name = property.Key.ToLowerInvariant();
                    if (name == "pan" || SensitiveNames.Any(name.Contains))
                    {
                        obj[property.Key] = "***";
                    }
                    else
                    {
                        Mask(property.Value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array) Mask(item);
                break;
        }
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

/// <summary>
/// Road distance estimate from coordinates: haversine distance x 1.3 (typical Indian road network factor).
/// Operations can override the distance when preparing the quotation.
/// </summary>
internal sealed class HaversineDistanceEstimator : IDistanceEstimator
{
    private const double RoadFactor = 1.3;

    public decimal? EstimateRoadDistanceKm(GeoPoint? from, GeoPoint? to)
    {
        if (from is not { IsValid: true } a || to is not { IsValid: true } b) return null;
        var km = a.DistanceKmTo(b) * RoadFactor;
        return km < 0.5 ? null : Math.Round((decimal)km, 1);
    }
}
