using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProCargo.Application.Authorization;
using ProCargo.Application.Common;
using ProCargo.Application.DTOs;
using ProCargo.Application.Exceptions;
using ProCargo.Application.Interfaces.Repositories;
using ProCargo.Application.Interfaces.Services;
using ProCargo.Application.Requests;
using ProCargo.Domain.Constants;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Services.Finance;

/// <summary>
/// Payments.
///
/// Security / integrity
///   * Card and UPI details are entered on the gateway's hosted checkout - never sent to ProCargo.
///   * Starting a payment is idempotent per customer + Idempotency-Key header (double clicks, retries).
///   * A payment becomes Paid only after a VERIFIED gateway signature: either the checkout signature returned to
///     the browser or the HMAC-signed webhook. Unsigned or tampered callbacks are rejected.
///   * Webhooks are idempotent per gateway event id (replays are acknowledged and ignored).
///   * Amounts can never exceed the invoice balance (checked again in SQL under a row lock).
///   * Refunds need ManageRefunds and cannot exceed the refundable amount (SQL).
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private const string OfflineGateway = "Offline";

    private readonly IPaymentRepository _payments;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentGateway _gateway;
    private readonly INotificationService _notifications;
    private readonly AccessGuard _access;
    private readonly IAuditLogger _audit;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _logger;
    private readonly ISandboxPaymentSimulator? _sandbox;

    public PaymentService(IPaymentRepository payments, IInvoiceRepository invoices, IPaymentGateway gateway,
        INotificationService notifications, AccessGuard access, IAuditLogger audit, IOptions<PaymentOptions> options,
        ILogger<PaymentService> logger, IEnumerable<ISandboxPaymentSimulator> sandbox)
    {
        _sandbox = sandbox.FirstOrDefault();
        _payments = payments;
        _invoices = invoices;
        _gateway = gateway;
        _notifications = notifications;
        _access = access;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public Task<PagedResult<PaymentListItemDto>> GetPagedAsync(PaymentSearchRequest request, CancellationToken cancellationToken)
    {
        var customerScope = _access.IsStaffWith(Permissions.ViewFinance) ? request.CustomerId : _access.RequireCustomerId();
        return _payments.GetPagedAsync(request, customerScope, cancellationToken);
    }

    public async Task<PaymentDetailsResponse> GetByIdAsync(long paymentId, CancellationToken cancellationToken)
    {
        var payment = await GetAccessibleAsync(paymentId, cancellationToken);
        var refunds = await _payments.GetRefundsAsync(paymentId, cancellationToken);
        return new PaymentDetailsResponse(payment, refunds);
    }

    public async Task<PaymentInitiatedResponse> InitiateAsync(InitiatePaymentRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!_access.User.HasPermission(Permissions.MakePayments)) throw new ForbiddenException();
        var customerId = _access.RequireCustomerId();

        var invoice = await _invoices.GetByIdAsync(request.InvoiceId, cancellationToken) ?? throw NotFoundException.For("Invoice", request.InvoiceId);
        if (invoice.CustomerId != customerId) throw NotFoundException.For("Invoice", request.InvoiceId);

        var amount = request.Amount ?? invoice.BalanceAmount;
        if (amount <= 0)
        {
            throw new BusinessRuleException("INVOICE_NOT_PAYABLE", "The invoice has no outstanding balance.");
        }

        var created = await _payments.CreateAsync(new CreatePaymentCommand(
            invoice.InvoiceId, amount, request.Method, PaymentStatus.Initiated, _gateway.Name, idempotencyKey, null, null,
            _access.User.UserId), cancellationToken);

        var payment = await _payments.GetByIdAsync(created.Id, cancellationToken) ?? throw NotFoundException.For("Payment", created.Id);

        if (created.IsExisting && payment.PaymentStatusId != (int)PaymentStatus.Initiated)
        {
            throw new ConflictException("PAYMENT_ALREADY_PROCESSED", "This payment request was already processed.");
        }

        string orderId;
        string? checkoutUrl = null;
        if (payment.GatewayOrderId is { Length: > 0 } existingOrder)
        {
            orderId = existingOrder;
        }
        else
        {
            var order = await _gateway.CreateOrderAsync(new GatewayOrderRequest(
                payment.PaymentId, payment.PaymentNumber, payment.Amount, _options.Currency, invoice.CustomerEmail), cancellationToken);
            await _payments.SetGatewayOrderAsync(payment.PaymentId, order.OrderId, cancellationToken);
            orderId = order.OrderId;
            checkoutUrl = order.CheckoutUrl;
        }

        if (!created.IsExisting)
        {
            await _audit.LogAsync("PaymentInitiated", "Payment", payment.PaymentId,
                newValue: new { payment.PaymentNumber, invoice.InvoiceNumber, payment.Amount, Gateway = _gateway.Name, OrderId = orderId },
                cancellationToken: cancellationToken);
        }

        return new PaymentInitiatedResponse(payment.PaymentId, payment.PaymentNumber, created.IsExisting,
            new GatewayCheckoutDto(_gateway.Name, orderId, _gateway.PublicKey, payment.Amount, _options.Currency, checkoutUrl));
    }

    public async Task<PaymentDto> ConfirmAsync(long paymentId, ConfirmPaymentRequest request, CancellationToken cancellationToken)
    {
        var payment = await GetAccessibleAsync(paymentId, cancellationToken);
        if (payment.CustomerId != _access.User.CustomerId) throw NotFoundException.For("Payment", paymentId);

        if (!string.Equals(payment.GatewayOrderId, request.GatewayOrderId, StringComparison.Ordinal)
            || !_gateway.VerifyCheckoutSignature(request.GatewayOrderId, request.GatewayPaymentId, request.Signature))
        {
            await _audit.LogAsync("PaymentSignatureRejected", "Payment", paymentId, cancellationToken: cancellationToken);
            throw new BusinessRuleException(ErrorCodes.WebhookSignatureInvalid, "The payment confirmation could not be verified.");
        }

        var result = await _payments.UpdateStatusAsync(new PaymentStatusUpdate(
            paymentId, PaymentStatus.Paid, request.GatewayPaymentId, $"checkout:{request.GatewayPaymentId}", "CHECKOUT_OK",
            "Verified checkout signature", _access.User.UserId), cancellationToken);

        if (!result.IsExisting)
        {
            await OnPaidAsync(paymentId, cancellationToken);
        }

        return await _payments.GetByIdAsync(paymentId, cancellationToken) ?? throw NotFoundException.For("Payment", paymentId);
    }

    public async Task<PaymentDto> SimulateSandboxCheckoutAsync(long paymentId, CancellationToken cancellationToken)
    {
        if (_sandbox is null) throw NotFoundException.For("Payment", paymentId);

        var payment = await GetAccessibleAsync(paymentId, cancellationToken);
        if (string.IsNullOrEmpty(payment.GatewayOrderId))
        {
            throw new BusinessRuleException("PAYMENT_NOT_INITIATED", "The payment has no gateway order.");
        }

        var (gatewayPaymentId, signature) = _sandbox.SimulateCheckout(payment.GatewayOrderId);
        return await ConfirmAsync(paymentId, new ConfirmPaymentRequest
        {
            GatewayOrderId = payment.GatewayOrderId,
            GatewayPaymentId = gatewayPaymentId,
            Signature = signature
        }, cancellationToken);
    }

    public async Task HandleWebhookAsync(string rawBody, string? signature, CancellationToken cancellationToken)
    {
        if (!_gateway.VerifyWebhookSignature(rawBody, signature))
        {
            _logger.LogWarning("Rejected payment webhook with an invalid signature");
            await _audit.LogAsync("PaymentWebhookRejected", "Payment", null, cancellationToken: cancellationToken);
            throw new UnauthorizedException(ErrorCodes.WebhookSignatureInvalid, "Invalid webhook signature.");
        }

        var evt = _gateway.ParseWebhook(rawBody);
        var payment = await _payments.GetByGatewayOrderAsync(_gateway.Name, evt.OrderId, cancellationToken);
        if (payment is null)
        {
            // Acknowledge so the gateway stops retrying; the order does not belong to this platform.
            _logger.LogWarning("Payment webhook {EventId} references unknown order {OrderId}", evt.EventId, evt.OrderId);
            return;
        }

        try
        {
            var result = await _payments.UpdateStatusAsync(new PaymentStatusUpdate(
                payment.PaymentId, evt.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed, evt.TransactionId, evt.EventId,
                evt.ResponseCode, evt.ResponseMessage, null), cancellationToken);

            if (result.IsExisting)
            {
                _logger.LogInformation("Duplicate payment webhook {EventId} ignored", evt.EventId);
                return;
            }

            await _audit.LogAsync(evt.IsSuccess ? "PaymentCaptured" : "PaymentFailed", "Payment", payment.PaymentId,
                newValue: new { evt.EventId, evt.TransactionId, evt.ResponseCode }, cancellationToken: cancellationToken);

            if (evt.IsSuccess)
            {
                await OnPaidAsync(payment.PaymentId, cancellationToken);
            }
        }
        catch (ConflictException ex) when (ex.ErrorCode == "PAYMENT_ALREADY_FINAL")
        {
            // e.g. a late "failed" event after the payment was captured: keep the final state.
            _logger.LogInformation("Payment webhook {EventId} ignored: payment already final", evt.EventId);
        }
    }

    public async Task<CreatedResponse> RecordOfflineAsync(RecordOfflinePaymentRequest request, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManagePayments);

        var created = await _payments.CreateAsync(new CreatePaymentCommand(
            request.InvoiceId, request.Amount, request.Method, PaymentStatus.Paid, OfflineGateway,
            idempotencyKey is null ? null : $"ops:{idempotencyKey}", request.ReferenceNumber.Trim(), request.Remarks?.Trim(),
            _access.User.UserId), cancellationToken);

        if (!created.IsExisting)
        {
            await _audit.LogAsync("PaymentRecorded", "Payment", created.Id,
                newValue: new { created.Number, request.InvoiceId, request.Amount, Method = request.Method.ToString(), request.ReferenceNumber },
                cancellationToken: cancellationToken);
            await OnPaidAsync(created.Id, cancellationToken);
        }

        return new CreatedResponse(created.Id, created.Number);
    }

    public async Task<CreatedResponse> RefundAsync(long paymentId, RefundPaymentRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ManageRefunds);
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken) ?? throw NotFoundException.For("Payment", paymentId);

        var refund = await _payments.CreateRefundAsync(paymentId, request.Amount, request.Reason.Trim(), _access.User.UserId, cancellationToken);

        var isOnline = payment.GatewayName == _gateway.Name && !string.IsNullOrEmpty(payment.GatewayTransactionId);
        GatewayRefundResult result;
        if (isOnline)
        {
            try
            {
                result = await _gateway.RefundAsync(payment.GatewayTransactionId!, request.Amount, request.Reason, cancellationToken);
            }
            catch (ExternalServiceException ex)
            {
                result = new GatewayRefundResult(false, null, ex.Message);
            }
        }
        else
        {
            // Offline payments are refunded by finance outside the platform; recording it completes the refund.
            result = new GatewayRefundResult(true, null, null);
        }

        await _payments.CompleteRefundAsync(refund.Id, result.IsSuccess, result.GatewayRefundId, result.FailureReason, _access.User.UserId,
            cancellationToken);
        await _audit.LogAsync(result.IsSuccess ? "RefundProcessed" : "RefundFailed", "Payment", paymentId,
            newValue: new { RefundNumber = refund.Number, request.Amount, request.Reason, result.GatewayRefundId, result.FailureReason },
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new ExternalServiceException(ErrorCodes.PaymentGatewayError,
                $"The refund {refund.Number} was recorded but the gateway rejected it: {result.FailureReason}");
        }

        return new CreatedResponse(refund.Id, refund.Number);
    }

    public Task<IReadOnlyList<ReconciliationRowDto>> GetReconciliationAsync(ReconciliationRequest request, CancellationToken cancellationToken)
    {
        _access.EnsureStaffPermission(Permissions.ViewFinance);
        return _payments.GetReconciliationAsync(request.FromUtc, request.ToUtc, cancellationToken);
    }

    private async Task<PaymentDto> GetAccessibleAsync(long paymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken) ?? throw NotFoundException.For("Payment", paymentId);
        _access.EnsureCustomer(payment.CustomerId, Permissions.ViewFinance, "Payment", paymentId);
        return payment;
    }

    private async Task OnPaidAsync(long paymentId, CancellationToken cancellationToken)
    {
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null) return;

        await _notifications.NotifyUserAsync(payment.CustomerUserId, NotificationTemplates.PaymentReceived, new Dictionary<string, string>
        {
            ["Name"] = payment.CustomerName,
            ["PaymentNumber"] = payment.PaymentNumber,
            ["InvoiceNumber"] = payment.InvoiceNumber,
            ["Amount"] = Formatting.Money(payment.Amount)
        }, new NotificationSubject("Payment", paymentId), cancellationToken);
    }
}
