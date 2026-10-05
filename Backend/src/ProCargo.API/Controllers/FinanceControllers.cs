using System.Text;
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
using ProCargo.Domain.Constants;

namespace ProCargo.API.Controllers;

[Route("api/v1/invoices")]
[Authorize]
public sealed class InvoicesController : ApiControllerBase
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    [HttpGet]
    public Task<PagedResult<InvoiceListItemDto>> Get([FromQuery] InvoiceSearchRequest request, CancellationToken cancellationToken) =>
        _invoices.GetPagedAsync(request, cancellationToken);

    [HttpGet("{invoiceId:long}")]
    public Task<InvoiceDetailsResponse> GetById(long invoiceId, CancellationToken cancellationToken) => _invoices.GetByIdAsync(invoiceId, cancellationToken);

    /// <summary>Issues the invoice for a delivered booking from its accepted quotation.</summary>
    [HttpPost]
    [HasPermission(Permissions.ManageInvoices)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var created = await _invoices.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { invoiceId = created.Id }, created);
    }

    [HttpPost("{invoiceId:long}/adjustments")]
    [HasPermission(Permissions.ManageInvoices)]
    public async Task<IActionResult> AddAdjustment(long invoiceId, InvoiceAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await _invoices.AddAdjustmentAsync(invoiceId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{invoiceId:long}/cancel")]
    [HasPermission(Permissions.ManageInvoices)]
    public async Task<IActionResult> Cancel(long invoiceId, CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        await _invoices.CancelAsync(invoiceId, request, cancellationToken);
        return NoContent();
    }
}

[Route("api/v1/payments")]
public sealed class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    [HttpGet]
    [Authorize]
    public Task<PagedResult<PaymentListItemDto>> Get([FromQuery] PaymentSearchRequest request, CancellationToken cancellationToken) =>
        _payments.GetPagedAsync(request, cancellationToken);

    [HttpGet("{paymentId:long}")]
    [Authorize]
    public Task<PaymentDetailsResponse> GetById(long paymentId, CancellationToken cancellationToken) => _payments.GetByIdAsync(paymentId, cancellationToken);

    /// <summary>
    /// Starts an online payment and returns the gateway checkout parameters. Send a unique Idempotency-Key header
    /// per payment attempt; retries with the same key return the same payment.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.MakePayments)]
    public Task<PaymentInitiatedResponse> Initiate(InitiatePaymentRequest request, CancellationToken cancellationToken)
    {
        var key = IdempotencyKey ?? throw new RequestValidationException("Idempotency-Key", "The Idempotency-Key header is required.");
        if (key.Length > 80) throw new RequestValidationException("Idempotency-Key", "The Idempotency-Key header is too long.");
        return _payments.InitiateAsync(request, key, cancellationToken);
    }

    /// <summary>Confirms a payment with the signed result the gateway checkout returned to the browser.</summary>
    [HttpPost("{paymentId:long}/confirm")]
    [HasPermission(Permissions.MakePayments)]
    public Task<PaymentDto> Confirm(long paymentId, ConfirmPaymentRequest request, CancellationToken cancellationToken) =>
        _payments.ConfirmAsync(paymentId, request, cancellationToken);

    /// <summary>Development only: completes a Sandbox checkout. Returns 404 when a real gateway is configured.</summary>
    [HttpPost("{paymentId:long}/sandbox/complete")]
    [HasPermission(Permissions.MakePayments)]
    public Task<PaymentDto> CompleteSandbox(long paymentId, CancellationToken cancellationToken) =>
        _payments.SimulateSandboxCheckoutAsync(paymentId, cancellationToken);

    /// <summary>Gateway webhook. Authenticated by the HMAC signature of the raw body, not by a user token.</summary>
    [HttpPost("webhooks/{gateway}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Webhook)]
    [RequestSizeLimit(256 * 1024)]
    public async Task<IActionResult> Webhook(string gateway, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault() ?? Request.Headers["X-Signature"].FirstOrDefault();
        await _payments.HandleWebhookAsync(body, signature, cancellationToken);
        return Ok();
    }

    /// <summary>Finance records a cash / cheque / bank-transfer payment.</summary>
    [HttpPost("offline")]
    [HasPermission(Permissions.ManagePayments)]
    public Task<CreatedResponse> RecordOffline(RecordOfflinePaymentRequest request, CancellationToken cancellationToken) =>
        _payments.RecordOfflineAsync(request, IdempotencyKey, cancellationToken);

    [HttpPost("{paymentId:long}/refunds")]
    [HasPermission(Permissions.ManageRefunds)]
    public Task<CreatedResponse> Refund(long paymentId, RefundPaymentRequest request, CancellationToken cancellationToken) =>
        _payments.RefundAsync(paymentId, request, cancellationToken);

    [HttpGet("reconciliation")]
    [HasPermission(Permissions.ViewFinance)]
    public Task<IReadOnlyList<ReconciliationRowDto>> GetReconciliation([FromQuery] ReconciliationRequest request, CancellationToken cancellationToken) =>
        _payments.GetReconciliationAsync(request, cancellationToken);
}

[Route("api/v1/settlements")]
[Authorize]
public sealed class SettlementsController : ApiControllerBase
{
    private readonly ISettlementService _settlements;

    public SettlementsController(ISettlementService settlements)
    {
        _settlements = settlements;
    }

    [HttpGet]
    public Task<PagedResult<SettlementListItemDto>> Get([FromQuery] SettlementSearchRequest request, CancellationToken cancellationToken) =>
        _settlements.GetPagedAsync(request, cancellationToken);

    [HttpGet("eligible-trips")]
    [HasPermission(Permissions.ManageSettlements)]
    public Task<PagedResult<SettlementEligibleTripDto>> GetEligibleTrips([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        _settlements.GetEligibleTripsAsync(request, cancellationToken);

    /// <summary>Owner earnings summary: own summary for owners, any owner for finance staff.</summary>
    [HttpGet("summary")]
    public Task<OwnerEarningsSummaryDto> GetSummary([FromQuery] long? ownerId, CancellationToken cancellationToken) =>
        _settlements.GetOwnerSummaryAsync(ownerId, cancellationToken);

    [HttpGet("{settlementId:long}")]
    public Task<SettlementDetailsResponse> GetById(long settlementId, CancellationToken cancellationToken) =>
        _settlements.GetByIdAsync(settlementId, cancellationToken);

    [HttpPost]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<ActionResult<CreatedResponse>> Create(CreateSettlementRequest request, CancellationToken cancellationToken)
    {
        var created = await _settlements.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { settlementId = created.Id }, created);
    }

    [HttpPost("{settlementId:long}/adjustments")]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<IActionResult> AddAdjustment(long settlementId, SettlementAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await _settlements.AddAdjustmentAsync(settlementId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Maker-checker: the approver must be a different user from the creator.</summary>
    [HttpPost("{settlementId:long}/approve")]
    [HasPermission(Permissions.ApproveSettlements)]
    public async Task<IActionResult> Approve(long settlementId, CancellationToken cancellationToken)
    {
        await _settlements.ApproveAsync(settlementId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{settlementId:long}/process")]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<IActionResult> Process(long settlementId, CancellationToken cancellationToken)
    {
        await _settlements.StartProcessingAsync(settlementId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{settlementId:long}/complete")]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<IActionResult> Complete(long settlementId, CompleteSettlementRequest request, CancellationToken cancellationToken)
    {
        await _settlements.CompleteAsync(settlementId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{settlementId:long}/fail")]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<IActionResult> Fail(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        await _settlements.FailAsync(settlementId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{settlementId:long}/cancel")]
    [HasPermission(Permissions.ManageSettlements)]
    public async Task<IActionResult> Cancel(long settlementId, SettlementReasonRequest request, CancellationToken cancellationToken)
    {
        await _settlements.CancelAsync(settlementId, request, cancellationToken);
        return NoContent();
    }
}
