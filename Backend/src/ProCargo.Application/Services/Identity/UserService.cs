using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Identity;

/// <summary>Internal user administration. Endpoints require the ManageUsers permission.</summary>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IAccountInviteService _invites;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public UserService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IAccountInviteService invites,
        ICurrentUser currentUser,
        IAuditLogger audit)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _invites = invites;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<PagedResult<UserListItemDto>> GetPagedAsync(UserSearchRequest request, CancellationToken cancellationToken) =>
        _users.GetPagedAsync(request, cancellationToken);

    public async Task<UserDetailsResponse> GetByIdAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw NotFoundException.For("User", userId);
        var roleIds = await _users.GetRoleIdsAsync(userId, cancellationToken);
        return new UserDetailsResponse(user, roleIds);
    }

    public async Task<long> CreateInternalAsync(CreateInternalUserRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        // Random unusable password; the user sets their own through the invite link.
        var command = new CreateInternalUserCommand(
            email,
            email.ToUpperInvariant(),
            IndianFormats.NormalizePhone(request.PhoneNumber),
            request.FullName.Trim(),
            _passwordHasher.Hash(_tokens.GenerateOpaqueToken()),
            request.RoleIds.Distinct().ToArray(),
            MustChangePassword: true,
            CreatedBy: _currentUser.UserId);

        var userId = await _users.CreateInternalAsync(command, cancellationToken);
        await _audit.LogAsync("UserCreated", "User", userId, newValue: new { email, request.RoleIds }, cancellationToken: cancellationToken);
        await _invites.SendInviteAsync(userId, PortalType.Operations, cancellationToken);
        return userId;
    }

    public async Task UpdateAsync(long userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _users.UpdateAsync(userId, request, IndianFormats.NormalizePhone(request.PhoneNumber), _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("UserUpdated", "User", userId, newValue: new { request.FullName }, cancellationToken: cancellationToken);
    }

    public async Task SetRolesAsync(long userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        if (userId == _currentUser.UserId)
        {
            throw new BusinessRuleException("CANNOT_CHANGE_OWN_ROLES", "You cannot change your own roles.");
        }

        var oldRoles = await _users.GetRoleIdsAsync(userId, cancellationToken);
        await _users.SetRolesAsync(userId, request.RoleIds.Distinct().ToArray(), _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("UserRolesChanged", "User", userId, oldValue: oldRoles, newValue: request.RoleIds, cancellationToken: cancellationToken);
    }

    public async Task SetActiveAsync(long userId, bool isActive, CancellationToken cancellationToken)
    {
        if (userId == _currentUser.UserId && !isActive)
        {
            throw new BusinessRuleException("CANNOT_DEACTIVATE_SELF", "You cannot deactivate your own account.");
        }

        await _users.SetActiveAsync(userId, isActive, _currentUser.UserId, cancellationToken);
        await _audit.LogAsync(isActive ? "UserActivated" : "UserDeactivated", "User", userId, cancellationToken: cancellationToken);
    }

    public async Task UnlockAsync(long userId, CancellationToken cancellationToken)
    {
        await _users.UnlockAsync(userId, _currentUser.UserId, cancellationToken);
        await _audit.LogAsync("UserUnlocked", "User", userId, cancellationToken: cancellationToken);
    }
}
