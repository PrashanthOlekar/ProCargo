using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Repositories;

public sealed record RegisterUserCommand(
    string Email,
    string NormalizedEmail,
    string PhoneNumber,
    string FullName,
    string PasswordHash,
    AccountType AccountType,
    CustomerType? CustomerType,
    string? CompanyName,
    OwnerType? OwnerType,
    string? BusinessName,
    string? GstNumber);

public sealed record CreateInternalUserCommand(
    string Email,
    string NormalizedEmail,
    string PhoneNumber,
    string FullName,
    string PasswordHash,
    IReadOnlyCollection<int> RoleIds,
    bool MustChangePassword,
    long CreatedBy);

public sealed record LoginAttempt(long? UserId, string AttemptedEmail, PortalType Portal, string? IpAddress, string? UserAgent);

/// <summary>Authentication persistence: users' credentials, login history, refresh and reset tokens.</summary>
public interface IIdentityRepository
{
    Task<UserAuthInfo?> GetAuthInfoByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<UserAuthInfo?> GetAuthInfoByIdAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermissionRow>> GetRolesAndPermissionsAsync(long userId, CancellationToken cancellationToken);
    Task<RegistrationResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);
    Task<bool> EnsureSuperAdminAsync(string email, string normalizedEmail, string phone, string fullName, string passwordHash, CancellationToken cancellationToken);

    Task RecordLoginSuccessAsync(LoginAttempt attempt, CancellationToken cancellationToken);
    Task<LoginFailureResult> RecordLoginFailureAsync(LoginAttempt attempt, string reason, bool countsTowardLockout,
        int maxFailedAttempts, int lockoutMinutes, CancellationToken cancellationToken);

    Task CreateRefreshTokenAsync(long userId, byte[] tokenHash, Guid familyId, PortalType portal, DateTime expiresUtc,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<RefreshTokenRecord?> GetRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);
    Task RotateRefreshTokenAsync(long oldTokenId, byte[] newTokenHash, DateTime expiresUtc, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken);
    Task RevokeRefreshTokenFamilyAsync(Guid familyId, string reason, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(byte[] tokenHash, string reason, CancellationToken cancellationToken);
    Task RevokeAllRefreshTokensAsync(long userId, string reason, CancellationToken cancellationToken);

    Task CreatePasswordResetTokenAsync(long userId, byte[] tokenHash, DateTime expiresUtc, string? ipAddress, CancellationToken cancellationToken);
    Task<long> ConsumePasswordResetTokenAsync(byte[] tokenHash, string newPasswordHash, CancellationToken cancellationToken);
    Task ChangePasswordAsync(long userId, string newPasswordHash, CancellationToken cancellationToken);
}

/// <summary>User administration (Operations portal).</summary>
public interface IUserRepository
{
    Task<PagedResult<UserListItemDto>> GetPagedAsync(UserSearchRequest request, CancellationToken cancellationToken);
    Task<UserDetailsDto?> GetByIdAsync(long userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> GetRoleIdsAsync(long userId, CancellationToken cancellationToken);
    Task<long> CreateInternalAsync(CreateInternalUserCommand command, CancellationToken cancellationToken);
    Task UpdateAsync(long userId, UpdateUserRequest request, string normalizedPhone, long modifiedBy, CancellationToken cancellationToken);
    Task SetRolesAsync(long userId, IReadOnlyCollection<int> roleIds, long modifiedBy, CancellationToken cancellationToken);
    Task SetActiveAsync(long userId, bool isActive, long modifiedBy, CancellationToken cancellationToken);
    Task UnlockAsync(long userId, long modifiedBy, CancellationToken cancellationToken);
}

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(int roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken);
    Task<int> CreateAsync(CreateRoleRequest request, long createdBy, CancellationToken cancellationToken);
    Task UpdateAsync(int roleId, UpdateRoleRequest request, long modifiedBy, CancellationToken cancellationToken);
    Task SetPermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds, long modifiedBy, CancellationToken cancellationToken);
    Task DeleteAsync(int roleId, CancellationToken cancellationToken);
}
