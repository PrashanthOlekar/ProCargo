using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResult> RefreshAsync(string refreshToken, PortalType portal, CancellationToken cancellationToken);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}

/// <summary>Sends "set your password" links to accounts created by someone else (staff users, drivers added by owners).</summary>
public interface IAccountInviteService
{
    Task SendInviteAsync(long userId, PortalType portal, CancellationToken cancellationToken);
}

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetPagedAsync(UserSearchRequest request, CancellationToken cancellationToken);
    Task<UserDetailsResponse> GetByIdAsync(long userId, CancellationToken cancellationToken);
    Task<long> CreateInternalAsync(CreateInternalUserRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken);
    Task SetRolesAsync(long userId, SetUserRolesRequest request, CancellationToken cancellationToken);
    Task SetActiveAsync(long userId, bool isActive, CancellationToken cancellationToken);
    Task UnlockAsync(long userId, CancellationToken cancellationToken);
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<RoleDetailsResponse> GetByIdAsync(int roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(CancellationToken cancellationToken);
    Task<int> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken);
    Task UpdateAsync(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken);
    Task SetPermissionsAsync(int roleId, SetRolePermissionsRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int roleId, CancellationToken cancellationToken);
}
