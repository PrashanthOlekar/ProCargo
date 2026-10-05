using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ProCargo.Application.DTOs;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Notifications;

/// <summary>
/// Renders sup.NotificationTemplate rows ({{Placeholder}} syntax) and delivers them per channel.
///
/// Privacy rules
///   * In-app notifications are stored with their text (they are the user's inbox).
///   * E-mail and SMS deliveries are recorded WITHOUT their body: those messages may carry password-reset links or
///     OTPs, which must never be persisted or logged. Only channel, template, status and failure reason are kept.
///   * Placeholder values are HTML-encoded for e-mail so user-supplied text cannot inject markup.
///   * Delivery failures never propagate: a provider outage must not roll back a business transaction.
/// </summary>
public sealed partial class NotificationService : INotificationService
{
    private static readonly TimeSpan TemplateCacheDuration = TimeSpan.FromMinutes(5);

    private readonly INotificationRepository _repository;
    private readonly IUserRepository _users;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(INotificationRepository repository, IUserRepository users, IEmailSender email, ISmsSender sms,
        IMemoryCache cache, ILogger<NotificationService> logger)
    {
        _repository = repository;
        _users = users;
        _email = email;
        _sms = sms;
        _cache = cache;
        _logger = logger;
    }

    public async Task NotifyUserAsync(long userId, string templateCode, IReadOnlyDictionary<string, string> data,
        NotificationSubject? subject = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var templates = await GetTemplatesAsync(templateCode, cancellationToken);
            if (templates.Count == 0)
            {
                _logger.LogWarning("Notification template {TemplateCode} has no active channel", templateCode);
                return;
            }

            var user = templates.Any(t => t.NotificationChannelId != (int)NotificationChannel.InApp)
                ? await _users.GetByIdAsync(userId, cancellationToken)
                : null;

            foreach (var template in templates)
            {
                await DeliverAsync(userId, user, template, data, subject, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Notification {TemplateCode} for user {UserId} failed", templateCode, userId);
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string templateCode, IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = (await GetTemplatesAsync(templateCode, cancellationToken))
                .FirstOrDefault(t => t.NotificationChannelId == (int)NotificationChannel.Sms);
            if (template is null)
            {
                _logger.LogWarning("SMS template {TemplateCode} is not configured", templateCode);
                return;
            }

            await _sms.SendAsync(phoneNumber, Render(template.Body, data, htmlEncode: false), cancellationToken);
            _logger.LogInformation("SMS {TemplateCode} sent", templateCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never log the message body or data: it contains the OTP.
            _logger.LogError(ex, "SMS {TemplateCode} delivery failed", templateCode);
        }
    }

    private async Task DeliverAsync(long userId, UserDetailsDto? user, NotificationTemplateDto template, IReadOnlyDictionary<string, string> data,
        NotificationSubject? subject, CancellationToken cancellationToken)
    {
        var channel = (NotificationChannel)template.NotificationChannelId;
        var title = Truncate(Render(template.Subject, data, htmlEncode: false), 200);

        switch (channel)
        {
            case NotificationChannel.InApp:
                await _repository.CreateAsync(new CreateNotificationCommand(userId, channel, template.TemplateCode, title,
                    Truncate(Render(template.Body, data, htmlEncode: false), 2000), NotificationStatus.Sent, subject?.EntityType,
                    subject?.EntityId, null), cancellationToken);
                return;

            case NotificationChannel.Email when user is not null:
                await SendExternalAsync(userId, channel, template, title, subject,
                    () => _email.SendAsync(new EmailMessage(user.Email, title, Render(template.Body, data, htmlEncode: true)), cancellationToken),
                    cancellationToken);
                return;

            case NotificationChannel.Sms when user is not null && !string.IsNullOrEmpty(user.PhoneNumber):
                await SendExternalAsync(userId, channel, template, title, subject,
                    () => _sms.SendAsync(user.PhoneNumber, Render(template.Body, data, htmlEncode: false), cancellationToken),
                    cancellationToken);
                return;

            default:
                // Push is reserved for the future mobile apps.
                return;
        }
    }

    private async Task SendExternalAsync(long userId, NotificationChannel channel, NotificationTemplateDto template, string title,
        NotificationSubject? subject, Func<Task> send, CancellationToken cancellationToken)
    {
        string? failure = null;
        try
        {
            await send();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = Truncate(ex.Message, 500);
            _logger.LogError(ex, "{Channel} notification {TemplateCode} for user {UserId} failed", channel, template.TemplateCode, userId);
        }

        var placeholder = channel == NotificationChannel.Email ? "[e-mail delivered externally]" : "[SMS delivered externally]";
        await _repository.CreateAsync(new CreateNotificationCommand(userId, channel, template.TemplateCode, title, placeholder,
            failure is null ? NotificationStatus.Sent : NotificationStatus.Failed, subject?.EntityType, subject?.EntityId, failure),
            cancellationToken);
    }

    private async Task<IReadOnlyList<NotificationTemplateDto>> GetTemplatesAsync(string templateCode, CancellationToken cancellationToken)
    {
        var key = "notification-template:" + templateCode;
        if (_cache.TryGetValue(key, out IReadOnlyList<NotificationTemplateDto>? cached) && cached is not null) return cached;

        var templates = (await _repository.GetTemplatesByCodeAsync(templateCode, cancellationToken)).Where(t => t.IsActive).ToList();
        _cache.Set(key, (IReadOnlyList<NotificationTemplateDto>)templates, TemplateCacheDuration);
        return templates;
    }

    internal static void InvalidateTemplates(IMemoryCache cache, string templateCode) => cache.Remove("notification-template:" + templateCode);

    internal static string Render(string template, IReadOnlyDictionary<string, string> data, bool htmlEncode) =>
        PlaceholderRegex().Replace(template, match =>
        {
            var value = data.TryGetValue(match.Groups[1].Value, out var v) ? v : string.Empty;
            return htmlEncode ? WebUtility.HtmlEncode(value) : value;
        });

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex PlaceholderRegex();
}
