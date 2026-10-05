using System.Globalization;
using Microsoft.Extensions.Options;
using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Identity;

/// <summary>
/// Accounts created by someone else (staff users, drivers onboarded by an owner) get a random password
/// nobody knows plus a one-time "set your password" link. This avoids ever e-mailing passwords.
/// </summary>
public sealed class AccountInviteService : IAccountInviteService
{
    private readonly IIdentityRepository _identity;
    private readonly ITokenService _tokens;
    private readonly INotificationService _notifications;
    private readonly SecurityOptions _security;
    private readonly PortalOptions _portals;
    private readonly TimeProvider _clock;

    public AccountInviteService(
        IIdentityRepository identity,
        ITokenService tokens,
        INotificationService notifications,
        IOptions<SecurityOptions> security,
        IOptions<PortalOptions> portals,
        TimeProvider clock)
    {
        _identity = identity;
        _tokens = tokens;
        _notifications = notifications;
        _security = security.Value;
        _portals = portals.Value;
        _clock = clock;
    }

    public async Task SendInviteAsync(long userId, PortalType portal, CancellationToken cancellationToken)
    {
        var user = await _identity.GetAuthInfoByIdAsync(userId, cancellationToken) ?? throw NotFoundException.For("User", userId);

        var token = _tokens.GenerateOpaqueToken();
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(_security.InviteTokenMinutes);
        await _identity.CreatePasswordResetTokenAsync(userId, _tokens.HashOpaqueToken(token), expires, null, cancellationToken);

        var baseUrl = portal == PortalType.Operations ? _portals.OperationsBaseUrl : _portals.WebBaseUrl;
        await _notifications.NotifyUserAsync(userId, NotificationTemplates.AccountInvite, new Dictionary<string, string>
        {
            ["Name"] = user.FullName,
            ["ResetLink"] = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}&invite=1",
            ["ExpiryMinutes"] = _security.InviteTokenMinutes.ToString(CultureInfo.InvariantCulture)
        }, cancellationToken: cancellationToken);
    }
}
