using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProCargo.API.Authorization;
using ProCargo.API.Extensions;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Application.Responses;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.API.Controllers;

/// <summary>
/// Sign-in, registration, token refresh and password management.
///
/// The access token is returned in the body and kept in memory by the SPA. The refresh token is set as an
/// HttpOnly, Secure, SameSite=Strict cookie scoped to /api/v1/auth (one cookie name per portal), so JavaScript
/// can never read it. Refresh and logout additionally require the X-ProCargo-Client header, which a cross-site
/// form post cannot add (CSRF defence in depth).
/// </summary>
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private const string CookiePath = "/api/v1/auth";
    private const string ClientHeader = "X-ProCargo-Client";

    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Self-registration for customers, vehicle owners and drivers.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.RegisterAsync(request, cancellationToken);
        SetRefreshCookie(PortalType.Web, result);
        return Ok(result.Response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(request, cancellationToken);
        SetRefreshCookie(request.Portal, result);
        return Ok(result.Response);
    }

    /// <summary>Rotates the refresh token (cookie) and returns a new access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        RequireClientHeader();
        var token = Request.Cookies[CookieName(request.Portal)];
        if (string.IsNullOrEmpty(token))
        {
            throw new UnauthorizedException(ErrorCodes.RefreshTokenInvalid, "Your session has expired. Please sign in again.");
        }

        try
        {
            var result = await _auth.RefreshAsync(token, request.Portal, cancellationToken);
            SetRefreshCookie(request.Portal, result);
            return Ok(result.Response);
        }
        catch (UnauthorizedException)
        {
            ClearRefreshCookie(request.Portal);
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<MessageResponse>> Logout(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        RequireClientHeader();
        await _auth.LogoutAsync(Request.Cookies[CookieName(request.Portal)], cancellationToken);
        ClearRefreshCookie(request.Portal);
        return Ok(MessageResponse.Ok("Signed out."));
    }

    /// <summary>Always answers the same way, whether or not the e-mail exists (no account enumeration).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _auth.ForgotPasswordAsync(request, cancellationToken);
        return Ok(MessageResponse.Ok("If an account exists for that e-mail, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<MessageResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _auth.ResetPasswordAsync(request, cancellationToken);
        return Ok(MessageResponse.Ok("Your password has been changed. Sign in with the new password."));
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<MessageResponse>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _auth.ChangePasswordAsync(request, cancellationToken);
        foreach (var portal in Enum.GetValues<PortalType>()) ClearRefreshCookie(portal);
        return Ok(MessageResponse.Ok("Password changed. Please sign in again."));
    }

    [HttpGet("me")]
    [Authorize]
    public Task<CurrentUserDto> Me(CancellationToken cancellationToken) => _auth.GetCurrentUserAsync(cancellationToken);

    private static string CookieName(PortalType portal) => portal == PortalType.Operations ? "pc_rt_ops" : "pc_rt_web";

    private void RequireClientHeader()
    {
        if (!Request.Headers.ContainsKey(ClientHeader))
        {
            throw new ForbiddenException("Missing client header.");
        }
    }

    private void SetRefreshCookie(PortalType portal, AuthResult result) =>
        Response.Cookies.Append(CookieName(portal), result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = result.RefreshTokenExpiresAtUtc,
            IsEssential = true
        });

    private void ClearRefreshCookie(PortalType portal) =>
        Response.Cookies.Delete(CookieName(portal), new CookieOptions { Path = CookiePath, Secure = true, SameSite = SameSiteMode.Strict });
}

[Route("api/v1/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    [HasPermission(Permissions.ManageUsers)]
    public Task<PagedResult<UserListItemDto>> Get([FromQuery] UserSearchRequest request, CancellationToken cancellationToken) =>
        _users.GetPagedAsync(request, cancellationToken);

    [HttpGet("{userId:long}")]
    [HasPermission(Permissions.ManageUsers)]
    public Task<UserDetailsResponse> GetById(long userId, CancellationToken cancellationToken) => _users.GetByIdAsync(userId, cancellationToken);

    /// <summary>Creates an internal staff user and e-mails them an invite link to set their password.</summary>
    [HttpPost]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateInternalUserRequest request, CancellationToken cancellationToken)
    {
        var id = await _users.CreateInternalAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = id }, new CreatedResponse(id, null));
    }

    [HttpPut("{userId:long}")]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<IActionResult> Update(long userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _users.UpdateAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId:long}/roles")]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<IActionResult> SetRoles(long userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        await _users.SetRolesAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:long}/activate")]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<IActionResult> Activate(long userId, CancellationToken cancellationToken)
    {
        await _users.SetActiveAsync(userId, true, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:long}/deactivate")]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<IActionResult> Deactivate(long userId, CancellationToken cancellationToken)
    {
        await _users.SetActiveAsync(userId, false, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:long}/unlock")]
    [HasPermission(Permissions.ManageUsers)]
    public async Task<IActionResult> Unlock(long userId, CancellationToken cancellationToken)
    {
        await _users.UnlockAsync(userId, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/roles")]
public sealed class RolesController : ApiControllerBase
{
    private readonly IRoleService _roles;

    public RolesController(IRoleService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    [HasAnyPermission(Permissions.ManageRoles, Permissions.ManageUsers)]
    public Task<IReadOnlyList<RoleDto>> Get(CancellationToken cancellationToken) => _roles.GetAllAsync(cancellationToken);

    [HttpGet("permissions")]
    [HasPermission(Permissions.ManageRoles)]
    public Task<IReadOnlyList<PermissionDto>> GetPermissions(CancellationToken cancellationToken) => _roles.GetAllPermissionsAsync(cancellationToken);

    [HttpGet("{roleId:int}")]
    [HasPermission(Permissions.ManageRoles)]
    public Task<RoleDetailsResponse> GetById(int roleId, CancellationToken cancellationToken) => _roles.GetByIdAsync(roleId, cancellationToken);

    [HttpPost]
    [HasPermission(Permissions.ManageRoles)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var id = await _roles.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { roleId = id }, new CreatedResponse(id, null));
    }

    [HttpPut("{roleId:int}")]
    [HasPermission(Permissions.ManageRoles)]
    public async Task<IActionResult> Update(int roleId, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        await _roles.UpdateAsync(roleId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{roleId:int}/permissions")]
    [HasPermission(Permissions.ManageRoles)]
    public async Task<IActionResult> SetPermissions(int roleId, SetRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        await _roles.SetPermissionsAsync(roleId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{roleId:int}")]
    [HasPermission(Permissions.ManageRoles)]
    public async Task<IActionResult> Delete(int roleId, CancellationToken cancellationToken)
    {
        await _roles.DeleteAsync(roleId, cancellationToken);
        return NoContent();
    }
}
