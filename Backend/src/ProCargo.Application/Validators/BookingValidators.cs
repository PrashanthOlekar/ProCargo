using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class BookingAddressRequestValidator : AbstractValidator<BookingAddressRequest>
{
    public BookingAddressRequestValidator()
    {
        RuleFor(x => x.AddressLine1).NotEmpty().SafeText(200);
        RuleFor(x => x.AddressLine2).SafeText(200);
        RuleFor(x => x.Landmark).SafeText(150);
        RuleFor(x => x.CityId).GreaterThan(0).WithMessage("Select a city.");
        RuleFor(x => x.Pincode).ValidPincode();
        RuleFor(x => x.Latitude).ValidLatitude();
        RuleFor(x => x.Longitude).ValidLongitude();
        RuleFor(x => x).Must(a => a.Latitude.HasValue == a.Longitude.HasValue)
            .WithName("Coordinates").WithMessage("Provide both latitude and longitude, or neither.");
    }
}

public sealed class BookingContactRequestValidator : AbstractValidator<BookingContactRequest>
{
    public BookingContactRequestValidator()
    {
        RuleFor(x => x.ContactName).ValidName();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.AlternatePhoneNumber).ValidOptionalPhone();
    }
}

public sealed class BookingItemRequestValidator : AbstractValidator<BookingItemRequest>
{
    public BookingItemRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().SafeText(200);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100000);
        RuleFor(x => x.WeightKg).GreaterThan(0).WithMessage("Weight must be greater than zero.").LessThanOrEqualTo(100000);
        RuleFor(x => x.LengthCm).GreaterThan(0).LessThan(10000).When(x => x.LengthCm.HasValue);
        RuleFor(x => x.WidthCm).GreaterThan(0).LessThan(10000).When(x => x.WidthCm.HasValue);
        RuleFor(x => x.HeightCm).GreaterThan(0).LessThan(10000).When(x => x.HeightCm.HasValue);
        RuleFor(x => x.DeclaredValue).GreaterThanOrEqualTo(0).When(x => x.DeclaredValue.HasValue);
    }
}

/// <summary>Shape validation. Business-rule checks (lead time, capacity, active master data) live in BookingService.</summary>
public abstract class BookingRequestBaseValidator<T> : AbstractValidator<T> where T : BookingRequestBase
{
    protected BookingRequestBaseValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0).WithMessage("Vehicle type is required.");
        RuleFor(x => x.GoodsTypeId).GreaterThan(0).WithMessage("Goods type is required.");
        RuleFor(x => x.GoodsDescription).NotEmpty().SafeText(500);
        RuleFor(x => x.SpecialInstructions).SafeText(1000);
        RuleFor(x => x.RequestedPickupDateUtc).NotEmpty();
        RuleFor(x => x.EstimatedDistanceKm).GreaterThan(0).LessThan(5000).When(x => x.EstimatedDistanceKm.HasValue);
        RuleFor(x => x.PickupAddress).NotNull().SetValidator(new BookingAddressRequestValidator());
        RuleFor(x => x.DeliveryAddress).NotNull().SetValidator(new BookingAddressRequestValidator());
        RuleFor(x => x.PickupContact).NotNull().SetValidator(new BookingContactRequestValidator());
        RuleFor(x => x.DeliveryContact).NotNull().SetValidator(new BookingContactRequestValidator());
        RuleFor(x => x.Items).NotEmpty().WithMessage("Add at least one item.");
        RuleForEach(x => x.Items).SetValidator(new BookingItemRequestValidator());
    }
}

public sealed class CreateBookingRequestValidator : BookingRequestBaseValidator<CreateBookingRequest>
{
}

public sealed class UpdateBookingRequestValidator : BookingRequestBaseValidator<UpdateBookingRequest>
{
    public UpdateBookingRequestValidator()
    {
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Please give a reason for cancelling.").SafeText(500);
    }
}

public sealed class BookingActionRequestValidator : AbstractValidator<BookingActionRequest>
{
    public BookingActionRequestValidator()
    {
        RuleFor(x => x.Remarks).SafeText(500);
    }
}

public sealed class AddBookingNoteRequestValidator : AbstractValidator<AddBookingNoteRequest>
{
    public AddBookingNoteRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().SafeText(1000);
    }
}
