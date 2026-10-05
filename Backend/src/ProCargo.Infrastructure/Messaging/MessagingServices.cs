using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Infrastructure.Messaging;

/// <summary>Section "Email".</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Smtp or None (log recipient + subject only).</summary>
    public string Provider { get; set; } = "Smtp";
    public string FromAddress { get; set; } = "no-reply@procargo.com";
    public string FromName { get; set; } = "ProCargo Logistics";
    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 1025;
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

/// <summary>Section "Sms".</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>Http (generic JSON provider API) or None (log masked number only).</summary>
    public string Provider { get; set; } = "None";
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? SenderId { get; set; }
}

/// <summary>
/// SMTP delivery (SendGrid / Azure Communication Services SMTP relay in production, smtp4dev locally).
/// The body is never logged because it can contain password-reset links.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrEmpty(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
        _logger.LogInformation("E-mail '{Subject}' sent", message.Subject);
    }
}

internal sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("E-mail delivery disabled; '{Subject}' not sent", message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Generic HTTP SMS gateway (MSG91 / Twilio / ACS style JSON API). Message text is never logged (OTPs).</summary>
internal sealed class HttpSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly SmsOptions _options;
    private readonly ILogger<HttpSmsSender> _logger;

    public HttpSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<HttpSmsSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiUrl)) throw new InvalidOperationException("Sms:ApiUrl is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
        {
            Content = JsonContent.Create(new { to = phoneNumber, sender = _options.SenderId, message })
        };
        request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException("SMS_FAILED", $"SMS provider returned {(int)response.StatusCode}.");
        }

        _logger.LogInformation("SMS sent to {Phone}", IndianFormats.MaskPhone(phoneNumber));
    }
}

internal sealed class NullSmsSender : ISmsSender
{
    private readonly ILogger<NullSmsSender> _logger;

    public NullSmsSender(ILogger<NullSmsSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SMS delivery disabled; message to {Phone} not sent", IndianFormats.MaskPhone(phoneNumber));
        return Task.CompletedTask;
    }
}
