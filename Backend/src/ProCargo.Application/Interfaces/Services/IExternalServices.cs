using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Interfaces.Services;

/// <summary>Object storage for uploaded files (Azure Blob Storage in production, local disk in development).</summary>
public interface IFileStorage
{
    /// <summary>Stores the content under <paramref name="storageKey"/> and returns the key.</summary>
    Task<string> SaveAsync(Stream content, string storageKey, string contentType, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken);
}

/// <summary>Estimates road distance when the customer did not provide one (straight line x road factor).</summary>
public interface IDistanceEstimator
{
    decimal? EstimateRoadDistanceKm(GeoPoint? from, GeoPoint? to);
}

/// <summary>Cached access to mst.SystemSetting business settings.</summary>
public interface ISettingsProvider
{
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken cancellationToken);
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue, CancellationToken cancellationToken);
    Task<string> GetStringAsync(string key, string defaultValue, CancellationToken cancellationToken);
    void Invalidate();
}

/// <summary>
/// Runs several repository calls in one database transaction. Used where orchestration happens in .NET
/// (e.g. storing file metadata + proof-of-delivery in one unit); single-procedure operations use SQL
/// transactions inside the procedure instead.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

/// <summary>Writes rows to aud.AuditLog with caller, IP, user agent and trace id.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, object? entityId, object? oldValue = null, object? newValue = null,
        long? userId = null, CancellationToken cancellationToken = default);
}
