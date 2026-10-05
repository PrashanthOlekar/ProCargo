using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.API.Authorization;

/// <summary>ICurrentUser built from the validated JWT of the current request (anonymous outside a request).</summary>
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public long UserId => UserIdOrNull ?? throw new UnauthorizedException(Application.Common.ErrorCodes.Unauthorized, "Authentication is required.");

    public long? UserIdOrNull => ReadLong(ProCargoClaimTypes.UserId);

    public string? Email => Principal?.FindFirst("email")?.Value;

    public string? FullName => Principal?.FindFirst("name")?.Value;

    public PortalType? Portal => Enum.TryParse<PortalType>(Principal?.FindFirst(ProCargoClaimTypes.Portal)?.Value, out var p) ? p : null;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ProCargoClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll(ProCargoClaimTypes.Permission).Select(c => c.Value).ToArray() ?? [];

    public long? CustomerId => ReadLong(ProCargoClaimTypes.CustomerId);

    public long? OwnerId => ReadLong(ProCargoClaimTypes.OwnerId);

    public long? DriverId => ReadLong(ProCargoClaimTypes.DriverId);

    public bool IsStaff => IsAuthenticated && Portal == PortalType.Operations;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(ProCargoClaimTypes.Permission, permission) == true
        && (Domain.Constants.Permissions.IsInternal(permission) ? IsStaff : Portal == PortalType.Web);

    public bool IsInRole(string role) => Principal?.HasClaim(ProCargoClaimTypes.Role, role) == true;

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _accessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    public string? TraceId => Activity.Current?.Id ?? _accessor.HttpContext?.TraceIdentifier;

    private long? ReadLong(string claim) =>
        long.TryParse(Principal?.FindFirst(claim)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
