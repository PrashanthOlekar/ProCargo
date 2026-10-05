namespace ProCargo.Application.Interfaces.Services;

public sealed record GatewayOrderRequest(long PaymentId, string PaymentNumber, decimal Amount, string Currency, string CustomerEmail);

/// <summary>Data the browser needs to open the gateway checkout. Never contains secrets.</summary>
public sealed record GatewayOrder(string GatewayName, string OrderId, string PublicKey, decimal Amount, string Currency, string? CheckoutUrl);

public sealed record GatewayWebhookEvent(
    string EventId,
    string OrderId,
    string? TransactionId,
    bool IsSuccess,
    string? ResponseCode,
    string? ResponseMessage);

public sealed record GatewayRefundResult(bool IsSuccess, string? GatewayRefundId, string? FailureReason);

/// <summary>
/// Provider-neutral payment gateway. Card / UPI data is entered on the provider's hosted checkout;
/// ProCargo never sees or stores it.
/// </summary>
public interface IPaymentGateway
{
    string Name { get; }

    /// <summary>Publishable key the browser checkout needs (never the secret).</summary>
    string PublicKey { get; }

    Task<GatewayOrder> CreateOrderAsync(GatewayOrderRequest request, CancellationToken cancellationToken);

    /// <summary>Verifies the webhook signature against the raw request body (HMAC).</summary>
    bool VerifyWebhookSignature(string rawBody, string? signature);

    GatewayWebhookEvent ParseWebhook(string rawBody);

    /// <summary>Verifies the signature the hosted checkout returns to the browser after a successful payment.</summary>
    bool VerifyCheckoutSignature(string orderId, string gatewayPaymentId, string signature);

    Task<GatewayRefundResult> RefundAsync(string gatewayTransactionId, decimal amount, string reason, CancellationToken cancellationToken);
}

/// <summary>
/// Development / test only: lets developers and automated tests complete a checkout without a real gateway.
/// Registered only when the Sandbox gateway is configured (never in Production).
/// </summary>
public interface ISandboxPaymentSimulator
{
    (string GatewayPaymentId, string Signature) SimulateCheckout(string orderId);
}
