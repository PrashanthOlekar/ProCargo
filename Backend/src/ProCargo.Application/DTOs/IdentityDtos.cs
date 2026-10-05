using ProCargo.Application.Common;

namespace ProCargo.Application.DTOs;

// Row shapes below mirror the columns returned by the sec.usp_* procedures exactly.

/// <summary>sec.usp_User_GetAuthInfo - internal to the auth service, never returned to clients.</summary>
public sealed class UserAuthInfo
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public Guid SecurityStamp { get; init; }
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; }
    public int FailedLoginCount { get; init; }
    public DateTime? LockoutEndUtc { get; init; }
    public bool MustChangePassword { get; init; }
    public long? CustomerId { get; init; }
    public long? OwnerId { get; init; }
    public long? DriverId { get; init; }
}

/// <summary>sec.usp_User_GetRolesAndPermissions</summary>
public sealed class RolePermissionRow
{
    public string Kind { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
}

/// <summary>sec.usp_User_RecordLoginFailure</summary>
public sealed class LoginFailureResult
{
    public int FailedLoginCount { get; init; }
    public DateTime? LockoutEndUtc { get; init; }
}

/// <summary>sec.usp_RefreshToken_GetByHash</summary>
public sealed class RefreshTokenRecord
{
    public long RefreshTokenId { get; init; }
    public long UserId { get; init; }
    public Guid FamilyId { get; init; }
    public string Portal { get; init; } = string.Empty;
    public DateTime ExpiresDateUtc { get; init; }
    public DateTime? RevokedDateUtc { get; init; }
    public long? ReplacedByTokenId { get; init; }
}

/// <summary>sec.usp_User_Register / core.usp_Driver_CreateWithUser</summary>
public sealed class RegistrationResult
{
    public long UserId { get; init; }
    public long ProfileId { get; init; }
    public string ProfileNumber { get; init; } = string.Empty;
}

/// <summary>The signed-in user as the front-ends see it (GET /auth/me and login responses).</summary>
public sealed record CurrentUserDto(
    long UserId,
    string Email,
    string FullName,
    string PhoneNumber,
    string Portal,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    long? CustomerId,
    long? OwnerId,
    long? DriverId,
    bool MustChangePassword);

/// <summary>Login / refresh response body. The refresh token itself travels only in an HttpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, CurrentUserDto User);

/// <summary>Service result: the response body plus the refresh token the controller writes to the cookie.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

/// <summary>sec.usp_User_GetPaged</summary>
public sealed class UserListItemDto : PagedRow
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; }
    public bool IsLocked { get; init; }
    public DateTime? LastLoginDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public string Roles { get; init; } = string.Empty;
}

/// <summary>sec.usp_User_GetById</summary>
public sealed class UserDetailsDto
{
    public long UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool PhoneConfirmed { get; init; }
    public DateTime? LockoutEndUtc { get; init; }
    public DateTime? LastLoginDateUtc { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public string Roles { get; init; } = string.Empty;
    public byte[] RowVersion { get; init; } = [];
}

public sealed record UserDetailsResponse(UserDetailsDto User, IReadOnlyList<long> RoleIds);

/// <summary>sec.usp_Role_GetAll</summary>
public sealed class RoleDto
{
    public int RoleId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public bool IsInternal { get; init; }
    public bool IsActive { get; init; }
    public int UserCount { get; init; }
    public int PermissionCount { get; init; }
}

/// <summary>sec.usp_Permission_GetAll / sec.usp_Role_GetPermissions</summary>
public sealed class PermissionDto
{
    public int PermissionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
}

public sealed record RoleDetailsResponse(RoleDto Role, IReadOnlyList<PermissionDto> Permissions);
