using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Services;

/// <summary>
/// The authenticated caller, built from the validated JWT. Implemented in the API layer from HttpContext.
/// Services use it for resource-level authorization (ownership checks) and auditing.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's user id. Throws when the request is anonymous.</summary>
    long UserId { get; }

    long? UserIdOrNull { get; }
    string? Email { get; }
    string? FullName { get; }
    PortalType? Portal { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }

    long? CustomerId { get; }
    long? OwnerId { get; }
    long? DriverId { get; }

    /// <summary>True for tokens issued to the Operations portal (internal staff).</summary>
    bool IsStaff { get; }

    bool HasPermission(string permission);
    bool IsInRole(string role);

    string? IpAddress { get; }
    string? UserAgent { get; }
    string? TraceId { get; }
}
