using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProCargo.Application.Common;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Services;

namespace ProCargo.Infrastructure.Payments;

/// <summary>Section "Payments:Razorpay" / "Payments:Sandbox". Secrets come from Key Vault / user-secrets.</summary>
public sealed class PaymentGatewayOptions
{
    public const string SectionName = "Payments";

    public RazorpayOptions Razorpay { get; set; } = new();
    public SandboxOptions Sandbox { get; set; } = new();

    public sealed class RazorpayOptions
    {
        public string BaseUrl { get; set; } = "https://api.razorpay.com/v1/";
        public string KeyId { get; set; } = string.Empty;
        public string KeySecret { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
    }

    public sealed class SandboxOptions
    {
        /// <summary>Secret used to sign simulated checkout results and webhooks.</summary>
        public string Secret { get; set; } = string.Empty;
    }
}

internal static class Hmac
{
    public static string Hex(string secret, string payload) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));

    public static bool Matches(string secret, string payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret)) return false;
        var expected = Encoding.UTF8.GetBytes(Hex(secret, payload));
        var provided = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}

/// <summary>
/// Razorpay (UPI, cards, net banking, wallets). Orders are created server-side so the amount cannot be tampered
/// with in the browser; checkout and webhook signatures are verified with HMAC-SHA256.
/// </summary>
internal sealed class RazorpayPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly PaymentGatewayOptions.RazorpayOptions _options;

    public RazorpayPaymentGateway(HttpClient http, IOptions<PaymentGatewayOptions> options)
    {
        _options = options.Value.Razorpay;
        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.KeyId}:{_options.KeySecret}")));
    }

    public string Name => "Razorpay";

    public string PublicKey => _options.KeyId;

    public async Task<GatewayOrder> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("orders", new
        {
            amount = ToPaise(request.Amount),
            currency = request.Currency,
            receipt = request.PaymentNumber,
            notes = new { paymentId = request.PaymentId.ToString(CultureInfo.InvariantCulture) }
        }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException(ErrorCodes.PaymentGatewayError, "The payment gateway could not create the order. Try again.");
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var orderId = json.RootElement.GetProperty("id").GetString()
                      ?? throw new ExternalServiceException(ErrorCodes.PaymentGatewayError, "Invalid gateway response.");
        return new GatewayOrder(Name, orderId, PublicKey, request.Amount, request.Currency, null);
    }

    public bool VerifyCheckoutSignature(string orderId, string gatewayPaymentId, string signature) =>
        Hmac.Matches(_options.KeySecret, $"{orderId}|{gatewayPaymentId}", signature);

    public bool VerifyWebhookSignature(string rawBody, string? signature) => Hmac.Matches(_options.WebhookSecret, rawBody, signature);

    public GatewayWebhookEvent ParseWebhook(string rawBody)
    {
        using var json = JsonDocument.Parse(rawBody);
        var root = json.RootElement;
        var eventName = root.GetProperty("event").GetString() ?? string.Empty;
        var payment = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");

        var paymentId = payment.GetProperty("id").GetString() ?? string.Empty;
        var orderId = payment.TryGetProperty("order_id", out var o) ? o.GetString() ?? string.Empty : string.Empty;
        var isSuccess = eventName is "payment.captured" or "order.paid";
        string? errorCode = payment.TryGetProperty("error_code", out var ec) && ec.ValueKind == JsonValueKind.String ? ec.GetString() : null;
        string? errorText = payment.TryGetProperty("error_description", out var ed) && ed.ValueKind == JsonValueKind.String ? ed.GetString() : null;

        return new GatewayWebhookEvent($"{eventName}:{paymentId}", orderId, paymentId, isSuccess, errorCode ?? eventName, errorText);
    }

    public async Task<GatewayRefundResult> RefundAsync(string gatewayTransactionId, decimal amount, string reason, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync($"payments/{Uri.EscapeDataString(gatewayTransactionId)}/refund",
            new { amount = ToPaise(amount), notes = new { reason } }, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GatewayRefundResult(false, null, $"Gateway returned {(int)response.StatusCode}");
        }

        using var json = JsonDocument.Parse(body);
        return new GatewayRefundResult(true, json.RootElement.GetProperty("id").GetString(), null);
    }

    private static long ToPaise(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Development gateway: behaves like Razorpay (orders, signed checkout results, signed webhooks, refunds) without
/// network calls. Startup refuses to use it in Production.
/// </summary>
internal sealed class SandboxPaymentGateway : IPaymentGateway, ISandboxPaymentSimulator
{
    private readonly string _secret;

    public SandboxPaymentGateway(IOptions<PaymentGatewayOptions> options)
    {
        _secret = string.IsNullOrWhiteSpace(options.Value.Sandbox.Secret) ? "procargo-sandbox-secret" : options.Value.Sandbox.Secret;
    }

    public string Name => "Sandbox";

    public string PublicKey => "sandbox";

    public Task<GatewayOrder> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new GatewayOrder(Name, $"sbx_order_{Guid.NewGuid():N}", PublicKey, request.Amount, request.Currency, null));

    public bool VerifyCheckoutSignature(string orderId, string gatewayPaymentId, string signature) =>
        Hmac.Matches(_secret, $"{orderId}|{gatewayPaymentId}", signature);

    public bool VerifyWebhookSignature(string rawBody, string? signature) => Hmac.Matches(_secret, rawBody, signature);

    public GatewayWebhookEvent ParseWebhook(string rawBody)
    {
        using var json = JsonDocument.Parse(rawBody);
        var root = json.RootElement;
        return new GatewayWebhookEvent(
            root.GetProperty("eventId").GetString() ?? string.Empty,
            root.GetProperty("orderId").GetString() ?? string.Empty,
            root.TryGetProperty("transactionId", out var t) ? t.GetString() : null,
            root.GetProperty("success").GetBoolean(),
            root.TryGetProperty("code", out var c) ? c.GetString() : null,
            root.TryGetProperty("message", out var m) ? m.GetString() : null);
    }

    public Task<GatewayRefundResult> RefundAsync(string gatewayTransactionId, decimal amount, string reason, CancellationToken cancellationToken) =>
        Task.FromResult(new GatewayRefundResult(true, $"sbx_rfnd_{Guid.NewGuid():N}", null));

    public (string GatewayPaymentId, string Signature) SimulateCheckout(string orderId)
    {
        var paymentId = $"sbx_pay_{Guid.NewGuid():N}";
        return (paymentId, Hmac.Hex(_secret, $"{orderId}|{paymentId}"));
    }
}
