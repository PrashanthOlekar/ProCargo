using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class PriceEstimateRequestValidator : AbstractValidator<PriceEstimateRequest>
{
    public PriceEstimateRequestValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.DistanceKm).GreaterThan(0).LessThanOrEqualTo(5000);
        RuleFor(x => x.WeightKg).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100000);
    }
}

public abstract class QuotationPricingRequestValidator<T> : AbstractValidator<T> where T : QuotationPricingRequest
{
    protected QuotationPricingRequestValidator()
    {
        RuleFor(x => x.DistanceKm).GreaterThan(0).LessThanOrEqualTo(5000).When(x => x.DistanceKm.HasValue);
        RuleFor(x => x.WaitingHours).InclusiveBetween(0, 240);
        RuleFor(x => x.TollAmount).InclusiveBetween(0, 1_000_000);
        RuleFor(x => x.DiscountAmount).InclusiveBetween(0, 10_000_000);
        RuleFor(x => x.ManualAdjustment).InclusiveBetween(-10_000_000, 10_000_000);
        RuleFor(x => x.ManualAdjustmentReason).NotEmpty().When(x => x.ManualAdjustment != 0)
            .WithMessage("Explain the manual adjustment.");
        RuleFor(x => x.ManualAdjustmentReason).SafeText(200);
    }
}

public sealed class PreviewQuotationRequestValidator : QuotationPricingRequestValidator<PreviewQuotationRequest>
{
    public PreviewQuotationRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
    }
}

public sealed class CreateQuotationRequestValidator : QuotationPricingRequestValidator<CreateQuotationRequest>
{
    public CreateQuotationRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.ValidityHours).InclusiveBetween(1, 720).When(x => x.ValidityHours.HasValue);
        RuleFor(x => x.Notes).SafeText(1000);
    }
}

public sealed class UpdateQuotationRequestValidator : QuotationPricingRequestValidator<UpdateQuotationRequest>
{
    public UpdateQuotationRequestValidator()
    {
        RuleFor(x => x.ValidityHours).InclusiveBetween(1, 720).When(x => x.ValidityHours.HasValue);
        RuleFor(x => x.Notes).SafeText(1000);
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class RejectQuotationRequestValidator : AbstractValidator<RejectQuotationRequest>
{
    public RejectQuotationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell us why you are rejecting the quotation.").SafeText(500);
    }
}

public sealed class WithdrawQuotationRequestValidator : AbstractValidator<WithdrawQuotationRequest>
{
    public WithdrawQuotationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().SafeText(500);
    }
}

public sealed class SaveVehiclePricingRequestValidator : AbstractValidator<SaveVehiclePricingRequest>
{
    public SaveVehiclePricingRequestValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.BaseFare).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumFare).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PerKmRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PerKgRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FreeWaitingHours).InclusiveBetween(0, 48);
        RuleFor(x => x.EffectiveToUtc).GreaterThan(x => x.EffectiveFromUtc).When(x => x.EffectiveToUtc.HasValue);
    }
}

public sealed class SaveDistancePricingRequestValidator : AbstractValidator<SaveDistancePricingRequest>
{
    public SaveDistancePricingRequestValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.FromKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ToKm).GreaterThan(x => x.FromKm).When(x => x.ToKm.HasValue);
        RuleFor(x => x.RatePerKm).GreaterThanOrEqualTo(0);
    }
}

public sealed class SaveAdditionalChargeRequestValidator : AbstractValidator<SaveAdditionalChargeRequest>
{
    private static readonly string[] Types = ["Flat", "PerHour", "PerKm", "Percentage"];

    public SaveAdditionalChargeRequestValidator()
    {
        RuleFor(x => x.ChargeCode).NotEmpty().MaximumLength(30).Matches("^[A-Za-z_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CalculationType).Must(Types.Contains).WithMessage("CalculationType must be Flat, PerHour, PerKm or Percentage.");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).LessThanOrEqualTo(100).When(x => x.CalculationType == "Percentage");
    }
}

public sealed class SavePricingRuleRequestValidator : AbstractValidator<SavePricingRuleRequest>
{
    public SavePricingRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AdjustmentType).Must(t => t is "Percentage" or "Flat");
        RuleFor(x => x.AdjustmentValue).InclusiveBetween(-90, 500).When(x => x.AdjustmentType == "Percentage");
        RuleFor(x => x.EffectiveToUtc).GreaterThan(x => x.EffectiveFromUtc).When(x => x.EffectiveToUtc.HasValue);
    }
}

public sealed class SaveTaxRateRequestValidator : AbstractValidator<SaveTaxRateRequest>
{
    public SaveTaxRateRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RatePercent).InclusiveBetween(0, 100);
        RuleFor(x => x.EffectiveToUtc).GreaterThan(x => x.EffectiveFromUtc).When(x => x.EffectiveToUtc.HasValue);
    }
}

public sealed class SaveCommissionRuleRequestValidator : AbstractValidator<SaveCommissionRuleRequest>
{
    public SaveCommissionRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CommissionPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.MinimumCommission).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EffectiveToUtc).GreaterThan(x => x.EffectiveFromUtc).When(x => x.EffectiveToUtc.HasValue);
    }
}
