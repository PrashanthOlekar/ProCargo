using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class CreateTripRequestValidator : AbstractValidator<CreateTripRequest>
{
    public CreateTripRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.PlannedPickupDateUtc)
            .GreaterThan(_ => clock.GetUtcNow().UtcDateTime.AddHours(-12))
            .WithMessage("Planned pickup cannot be in the past.");
        RuleFor(x => x.PlannedDeliveryDateUtc)
            .GreaterThan(x => x.PlannedPickupDateUtc)
            .WithMessage("Planned delivery must be after planned pickup.");
    }
}

public sealed class ReassignVehicleRequestValidator : AbstractValidator<ReassignVehicleRequest>
{
    public ReassignVehicleRequestValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().SafeText(300);
    }
}

public sealed class ReassignDriverRequestValidator : AbstractValidator<ReassignDriverRequest>
{
    public ReassignDriverRequestValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().SafeText(300);
    }
}

public sealed class TripReasonRequestValidator : AbstractValidator<TripReasonRequest>
{
    public TripReasonRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().SafeText(500);
    }
}

public sealed class StartTripRequestValidator : AbstractValidator<StartTripRequest>
{
    public StartTripRequestValidator()
    {
        RuleFor(x => x.Remarks).SafeText(500);
    }
}

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Otp).NotEmpty().Matches("^[0-9]{6}$").WithMessage("Enter the 6-digit OTP.");
        RuleFor(x => x.ReceiverName).SafeText(150);
        RuleFor(x => x.Odometer).InclusiveBetween(0, 9_999_999).When(x => x.Odometer.HasValue);
        RuleFor(x => x.Latitude).ValidLatitude();
        RuleFor(x => x.Longitude).ValidLongitude();
    }
}

public sealed class AddLocationsRequestValidator : AbstractValidator<AddLocationsRequest>
{
    public AddLocationsRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.Points).NotEmpty().Must(p => p.Count <= 100).WithMessage("Send at most 100 points per request.");
        RuleForEach(x => x.Points).ChildRules(point =>
        {
            point.RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
            point.RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
            point.RuleFor(p => p.RecordedDateUtc)
                .LessThanOrEqualTo(_ => clock.GetUtcNow().UtcDateTime.AddMinutes(5))
                .GreaterThan(_ => clock.GetUtcNow().UtcDateTime.AddDays(-2))
                .WithMessage("Location timestamps must be recent.");
            point.RuleFor(p => p.SpeedKmph).InclusiveBetween(0, 300).When(p => p.SpeedKmph.HasValue);
            point.RuleFor(p => p.Heading).InclusiveBetween(0, 360).When(p => p.Heading.HasValue);
            point.RuleFor(p => p.Accuracy).InclusiveBetween(0, 100000).When(p => p.Accuracy.HasValue);
        });
    }
}

public sealed class UploadProofOfDeliveryRequestValidator : AbstractValidator<UploadProofOfDeliveryRequest>
{
    public UploadProofOfDeliveryRequestValidator()
    {
        RuleFor(x => x.ReceiverName).ValidName();
        RuleFor(x => x.ReceiverPhone).ValidOptionalPhone();
        RuleFor(x => x.Remarks).SafeText(1000);
        RuleFor(x => x.Latitude).ValidLatitude();
        RuleFor(x => x.Longitude).ValidLongitude();
    }
}
