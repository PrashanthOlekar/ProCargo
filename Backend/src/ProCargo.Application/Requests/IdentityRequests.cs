using ProCargo.Application.Common;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Requests;

public sealed class RegisterRequest
{
    public AccountType AccountType { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public CustomerType? CustomerType { get; set; }
    public string? CompanyName { get; set; }
    public OwnerType? OwnerType { get; set; }
    public string? BusinessName { get; set; }
    public string? GstNumber { get; set; }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public PortalType Portal { get; set; } = PortalType.Web;
}

public sealed class RefreshTokenRequest
{
    public PortalType Portal { get; set; } = PortalType.Web;
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public PortalType Portal { get; set; } = PortalType.Web;
}

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UserSearchRequest : PagedRequest
{
    public bool? IsInternal { get; set; }
    public int? RoleId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class CreateInternalUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public List<int> RoleIds { get; set; } = [];
}

public sealed class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class SetUserRolesRequest
{
    public List<int> RoleIds { get; set; } = [];
}

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInternal { get; set; } = true;
}

public sealed class UpdateRoleRequest
{
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SetRolePermissionsRequest
{
    public List<int> PermissionIds { get; set; } = [];
}
