using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Services.Identity;

/// <summary>
/// Authentication use cases: registration, login, refresh-token rotation, logout and password flows.
///
/// Security notes
///  * Unknown e-mail and wrong password produce the same error and take the same time (dummy hash check).
///  * Failed attempts are counted per account; the account locks after N failures for M minutes.
///  * Refresh tokens are opaque, stored as SHA-256 hashes, rotated on every use and grouped in families.
///    Presenting an already-rotated token is treated as theft: the whole family is revoked.
///  * A token is bound to the portal it was issued for; staff cannot sign in to the customer portal and
///    external users can never obtain an Operations token.
/// </summary>
public sealed class AuthService : IAuthService
{
    // Hash of a random password, used to equalise timing when the e-mail does not exist.
    private const string DummyPasswordHash =
        "AQAAAAIAAYagAAAAEPl8QxzjG3NAMV98+zEreBua3BldwzeH88OdU3jP0VqWAZVEzQx7l6C/I9jTZOfMfA==";

    private readonly IIdentityRepository _identity;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly SecurityOptions _security;
    private readonly PortalOptions _portals;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IIdentityRepository identity,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        ICurrentUser currentUser,
        INotificationService notifications,
        IAuditLogger audit,
        IOptions<SecurityOptions> security,
        IOptions<PortalOptions> portals,
        TimeProvider clock,
        ILogger<AuthService> logger)
    {
        _identity = identity;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _currentUser = currentUser;
        _notifications = notifications;
        _audit = audit;
        _security = security.Value;
        _portals = portals.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var command = new RegisterUserCommand(
            Email: email,
            NormalizedEmail: Normalize(email),
            PhoneNumber: IndianFormats.NormalizePhone(request.PhoneNumber),
            FullName: request.FullName.Trim(),
            PasswordHash: _passwordHasher.Hash(request.Password),
            AccountType: request.AccountType,
            CustomerType: request.CustomerType,
            CompanyName: request.CompanyName?.Trim(),
            OwnerType: request.OwnerType,
            BusinessName: request.BusinessName?.Trim(),
            GstNumber: request.GstNumber?.Trim().ToUpperInvariant());

        var registration = await _identity.RegisterAsync(command, cancellationToken);

        await _audit.LogAsync("UserRegistered", "User", registration.UserId,
            newValue: new { request.AccountType, registration.ProfileNumber }, userId: registration.UserId, cancellationToken: cancellationToken);

        await _notifications.NotifyUserAsync(registration.UserId, NotificationTemplates.Welcome,
            new Dictionary<string, string> { ["Name"] = command.FullName }, cancellationToken: cancellationToken);

        _logger.LogInformation("New {AccountType} account {UserId} registered", request.AccountType, registration.UserId);

        var user = await _identity.GetAuthInfoByIdAsync(registration.UserId, cancellationToken)
                   ?? throw NotFoundException.For("User", registration.UserId);

        return await IssueSessionAsync(user, PortalType.Web, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await _identity.GetAuthInfoByEmailAsync(Normalize(email), cancellationToken);

        if (user is null)
        {
            _passwordHasher.Verify(DummyPasswordHash, request.Password);
            await RecordFailureAsync(null, email, request.Portal, "UnknownUser", countsTowardLockout: false, cancellationToken);
            throw InvalidCredentials();
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (user.LockoutEndUtc > now)
        {
            await RecordFailureAsync(user.UserId, email, request.Portal, "LockedOut", countsTowardLockout: false, cancellationToken);
            throw new UnauthorizedException(ErrorCodes.AccountLocked,
                "Too many failed attempts. The account is temporarily locked; try again later or reset your password.");
        }

        var check = _passwordHasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheckResult.Failed)
        {
            var failure = await RecordFailureAsync(user.UserId, email, request.Portal, "InvalidPassword", countsTowardLockout: true, cancellationToken);
            if (failure.LockoutEndUtc > now)
            {
                await _audit.LogAsync("AccountLockedOut", "User", user.UserId, userId: user.UserId, cancellationToken: cancellationToken);
            }

            throw InvalidCredentials();
        }

        if (!user.IsActive)
        {
            await RecordFailureAsync(user.UserId, email, request.Portal, "Inactive", countsTowardLockout: false, cancellationToken);
            throw new UnauthorizedException(ErrorCodes.AccountInactive, "This account has been deactivated. Contact support.");
        }

        if (check == PasswordCheckResult.SuccessRehashNeeded)
        {
            await _identity.ChangePasswordAsync(user.UserId, _passwordHasher.Hash(request.Password), cancellationToken);
        }

        var result = await IssueSessionAsync(user, request.Portal, cancellationToken);

        await _identity.RecordLoginSuccessAsync(
            new LoginAttempt(user.UserId, email, request.Portal, _currentUser.IpAddress, _currentUser.UserAgent), cancellationToken);
        await _audit.LogAsync("Login", "User", user.UserId, newValue: new { Portal = request.Portal.ToString() },
            userId: user.UserId, cancellationToken: cancellationToken);

        return result;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, PortalType portal, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw InvalidRefreshToken();
        }

        var hash = _tokens.HashOpaqueToken(refreshToken);
        var stored = await _identity.GetRefreshTokenAsync(hash, cancellationToken) ?? throw InvalidRefreshToken();
        var now = _clock.GetUtcNow().UtcDateTime;

        if (stored.RevokedDateUtc is not null)
        {
            if (stored.ReplacedByTokenId is not null)
            {
                // A rotated token was presented again: likely stolen. Kill the whole session family.
                await _identity.RevokeRefreshTokenFamilyAsync(stored.FamilyId, "ReuseDetected", cancellationToken);
                await _audit.LogAsync("RefreshTokenReuseDetected", "User", stored.UserId, userId: stored.UserId, cancellationToken: cancellationToken);
                _logger.LogWarning("Refresh token reuse detected for user {UserId}; session family revoked", stored.UserId);
            }

            throw InvalidRefreshToken();
        }

        if (stored.ExpiresDateUtc <= now || !string.Equals(stored.Portal, portal.ToString(), StringComparison.Ordinal))
        {
            throw InvalidRefreshToken();
        }

        var user = await _identity.GetAuthInfoByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.LockoutEndUtc > now)
        {
            await _identity.RevokeRefreshTokenFamilyAsync(stored.FamilyId, "UserUnavailable", cancellationToken);
            throw InvalidRefreshToken();
        }

        var subject = await BuildSubjectAsync(user, portal, cancellationToken);
        var newRefreshToken = _tokens.GenerateOpaqueToken();
        var refreshExpires = now.Add(_tokens.GetRefreshTokenLifetime(portal));

        try
        {
            await _identity.RotateRefreshTokenAsync(stored.RefreshTokenId, _tokens.HashOpaqueToken(newRefreshToken), refreshExpires,
                _currentUser.IpAddress, _currentUser.UserAgent, cancellationToken);
        }
        catch (ConflictException)
        {
            // Lost a race with a concurrent refresh using the same token.
            throw InvalidRefreshToken();
        }

        var access = _tokens.CreateAccessToken(subject);
        return new AuthResult(new AuthResponse(access.Token, access.ExpiresAtUtc, ToDto(user, subject)), newRefreshToken, refreshExpires);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _identity.RevokeRefreshTokenAsync(_tokens.HashOpaqueToken(refreshToken), "Logout", cancellationToken);
        }

        if (_currentUser.IsAuthenticated)
        {
            await _audit.LogAsync("Logout", "User", _currentUser.UserId, cancellationToken: cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Always behaves the same whether or not the account exists (no account enumeration).
        var user = await _identity.GetAuthInfoByEmailAsync(Normalize(request.Email), cancellationToken);
        if (user is null || !user.IsActive || user.IsInternal != (request.Portal == PortalType.Operations))
        {
            _logger.LogInformation("Password reset requested for an unknown or ineligible account");
            return;
        }

        var token = _tokens.GenerateOpaqueToken();
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(_security.PasswordResetTokenMinutes);
        await _identity.CreatePasswordResetTokenAsync(user.UserId, _tokens.HashOpaqueToken(token), expires, _currentUser.IpAddress, cancellationToken);

        var baseUrl = request.Portal == PortalType.Operations ? _portals.OperationsBaseUrl : _portals.WebBaseUrl;
        await _notifications.NotifyUserAsync(user.UserId, NotificationTemplates.PasswordReset, new Dictionary<string, string>
        {
            ["Name"] = user.FullName,
            ["ResetLink"] = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}",
            ["ExpiryMinutes"] = _security.PasswordResetTokenMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }, cancellationToken: cancellationToken);

        await _audit.LogAsync("PasswordResetRequested", "User", user.UserId, userId: user.UserId, cancellationToken: cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = await _identity.ConsumePasswordResetTokenAsync(
            _tokens.HashOpaqueToken(request.Token), _passwordHasher.Hash(request.NewPassword), cancellationToken);

        await _audit.LogAsync("PasswordReset", "User", userId, userId: userId, cancellationToken: cancellationToken);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _identity.GetAuthInfoByIdAsync(_currentUser.UserId, cancellationToken)
                   ?? throw NotFoundException.For("User", _currentUser.UserId);

        if (_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            throw new BusinessRuleException(ErrorCodes.CurrentPasswordInvalid, "The current password is incorrect.");
        }

        await _identity.ChangePasswordAsync(user.UserId, _passwordHasher.Hash(request.NewPassword), cancellationToken);
        await _audit.LogAsync("PasswordChanged", "User", user.UserId, cancellationToken: cancellationToken);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await _identity.GetAuthInfoByIdAsync(_currentUser.UserId, cancellationToken)
                   ?? throw NotFoundException.For("User", _currentUser.UserId);
        var subject = await BuildSubjectAsync(user, _currentUser.Portal ?? PortalType.Web, cancellationToken);
        return ToDto(user, subject);
    }

    private async Task<AuthResult> IssueSessionAsync(UserAuthInfo user, PortalType portal, CancellationToken cancellationToken)
    {
        var subject = await BuildSubjectAsync(user, portal, cancellationToken);
        var access = _tokens.CreateAccessToken(subject);

        var refreshToken = _tokens.GenerateOpaqueToken();
        var refreshExpires = _clock.GetUtcNow().UtcDateTime.Add(_tokens.GetRefreshTokenLifetime(portal));
        await _identity.CreateRefreshTokenAsync(user.UserId, _tokens.HashOpaqueToken(refreshToken), Guid.NewGuid(), portal,
            refreshExpires, _currentUser.IpAddress, _currentUser.UserAgent, cancellationToken);

        return new AuthResult(new AuthResponse(access.Token, access.ExpiresAtUtc, ToDto(user, subject)), refreshToken, refreshExpires);
    }

    /// <summary>Loads roles/permissions and enforces the portal boundary.</summary>
    private async Task<TokenSubject> BuildSubjectAsync(UserAuthInfo user, PortalType portal, CancellationToken cancellationToken)
    {
        var rows = await _identity.GetRolesAndPermissionsAsync(user.UserId, cancellationToken);
        var isOperations = portal == PortalType.Operations;

        var roles = rows.Where(r => r.Kind == "Role" && r.IsInternal == isOperations).Select(r => r.Code).Distinct().ToArray();
        var permissions = rows.Where(r => r.Kind == "Permission" && r.IsInternal == isOperations).Select(r => r.Code).Distinct().ToArray();

        var allowed = isOperations
            ? user.IsInternal && permissions.Contains(Permissions.AccessOperationsPortal)
            : !user.IsInternal && roles.Length > 0;

        if (!allowed)
        {
            await RecordFailureAsync(user.UserId, user.Email, portal, "PortalNotAllowed", countsTowardLockout: false, cancellationToken);
            throw new UnauthorizedException(ErrorCodes.PortalNotAllowed, isOperations
                ? "This account cannot sign in to the Operations portal."
                : "Staff accounts must sign in through the Operations portal.");
        }

        return new TokenSubject(user.UserId, user.Email, user.FullName, portal, roles, permissions,
            user.CustomerId, user.OwnerId, user.DriverId, user.MustChangePassword);
    }

    private Task<LoginFailureResult> RecordFailureAsync(long? userId, string email, PortalType portal, string reason,
        bool countsTowardLockout, CancellationToken cancellationToken) =>
        _identity.RecordLoginFailureAsync(
            new LoginAttempt(userId, email.Length > 256 ? email[..256] : email, portal, _currentUser.IpAddress, _currentUser.UserAgent),
            reason, countsTowardLockout, _security.MaxFailedLoginAttempts, _security.LockoutMinutes, cancellationToken);

    private static CurrentUserDto ToDto(UserAuthInfo user, TokenSubject subject) =>
        new(user.UserId, user.Email, user.FullName, user.PhoneNumber, subject.Portal.ToString(), subject.Roles, subject.Permissions,
            user.CustomerId, user.OwnerId, user.DriverId, user.MustChangePassword);

    private static string Normalize(string email) => email.Trim().ToUpperInvariant();

    private static UnauthorizedException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "Invalid e-mail or password.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new(ErrorCodes.RefreshTokenInvalid, "Your session has expired. Please sign in again.");
}
