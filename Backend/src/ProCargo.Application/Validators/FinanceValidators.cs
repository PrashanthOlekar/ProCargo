using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 120).When(x => x.PaymentTermsDays.HasValue);
    }
}

public sealed class InvoiceAdjustmentRequestValidator : AbstractValidator<InvoiceAdjustmentRequest>
{
    public InvoiceAdjustmentRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().SafeText(200);
        RuleFor(x => x.Amount).NotEqual(0).InclusiveBetween(-10_000_000, 10_000_000);
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class CancelInvoiceRequestValidator : AbstractValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().SafeText(500);
    }
}

public sealed class InitiatePaymentRequestValidator : AbstractValidator<InitiatePaymentRequest>
{
    public InitiatePaymentRequestValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000).When(x => x.Amount.HasValue);
        RuleFor(x => x.Method).IsInEnum()
            .Must(m => m is Domain.Enums.PaymentMethod.Upi or Domain.Enums.PaymentMethod.Card
                or Domain.Enums.PaymentMethod.NetBanking or Domain.Enums.PaymentMethod.Wallet)
            .WithMessage("Online payments support UPI, card, net banking and wallets.");
    }
}

public sealed class ConfirmPaymentRequestValidator : AbstractValidator<ConfirmPaymentRequest>
{
    public ConfirmPaymentRequestValidator()
    {
        RuleFor(x => x.GatewayOrderId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GatewayPaymentId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Signature).NotEmpty().MaximumLength(256);
    }
}

public sealed class RecordOfflinePaymentRequestValidator : AbstractValidator<RecordOfflinePaymentRequest>
{
    public RecordOfflinePaymentRequestValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.Method).IsInEnum()
            .Must(m => m is Domain.Enums.PaymentMethod.Cash or Domain.Enums.PaymentMethod.Cheque or Domain.Enums.PaymentMethod.BankTransfer)
            .WithMessage("Offline payments are cash, cheque or bank transfer.");
        RuleFor(x => x.ReferenceNumber).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9/_\\-. ]+$");
        RuleFor(x => x.Remarks).SafeText(500);
    }
}

public sealed class RefundPaymentRequestValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(10_000_000);
        RuleFor(x => x.Reason).NotEmpty().SafeText(500);
    }
}

public sealed class ReconciliationRequestValidator : AbstractValidator<ReconciliationRequest>
{
    public ReconciliationRequestValidator()
    {
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
        RuleFor(x => x).Must(x => (x.ToUtc - x.FromUtc).TotalDays <= 366).WithMessage("The period can be at most one year.");
    }
}

public sealed class CreateSettlementRequestValidator : AbstractValidator<CreateSettlementRequest>
{
    public CreateSettlementRequestValidator()
    {
        RuleFor(x => x.TripId).GreaterThan(0);
    }
}

public sealed class SettlementAdjustmentRequestValidator : AbstractValidator<SettlementAdjustmentRequest>
{
    public SettlementAdjustmentRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().SafeText(200);
        RuleFor(x => x.Amount).NotEqual(0).InclusiveBetween(-10_000_000, 10_000_000);
    }
}

public sealed class CompleteSettlementRequestValidator : AbstractValidator<CompleteSettlementRequest>
{
    public CompleteSettlementRequestValidator()
    {
        RuleFor(x => x.TransactionReference).NotEmpty().MaximumLength(100).Matches("^[A-Za-z0-9/_\\-.]+$");
    }
}

public sealed class SettlementReasonRequestValidator : AbstractValidator<SettlementReasonRequest>
{
    public SettlementReasonRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().SafeText(500);
    }
}
