namespace ProCargo.Application.Interfaces.Services;

/// <summary>Optional link from a notification to the record it is about (for deep links in the portals).</summary>
public sealed record NotificationSubject(string EntityType, long EntityId);

/// <summary>
/// Renders templates (sup.NotificationTemplate) and delivers them on every active channel of the template:
/// in-app (stored), e-mail and SMS. Delivery failures are recorded, never thrown to the caller, so a
/// business transaction is not rolled back because an e-mail provider is down.
/// </summary>
public interface INotificationService
{
    Task NotifyUserAsync(long userId, string templateCode, IReadOnlyDictionary<string, string> data,
        NotificationSubject? subject = null, CancellationToken cancellationToken = default);

    /// <summary>Sends an SMS-only template to a phone number that may not belong to a user (e.g. a delivery contact).</summary>
    Task SendSmsAsync(string phoneNumber, string templateCode, IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
