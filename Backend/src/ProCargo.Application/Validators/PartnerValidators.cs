using FluentValidation;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Validators;

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.CustomerType).IsInEnum();
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200).When(x => x.CustomerType == CustomerType.Business);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.GstNumber).ValidOptionalGst();
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class SaveCustomerAddressRequestValidator : AbstractValidator<SaveCustomerAddressRequest>
{
    public SaveCustomerAddressRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AddressLine1).NotEmpty().SafeText(200);
        RuleFor(x => x.AddressLine2).SafeText(200);
        RuleFor(x => x.Landmark).SafeText(150);
        RuleFor(x => x.CityId).GreaterThan(0);
        RuleFor(x => x.Pincode).ValidPincode();
        RuleFor(x => x.Latitude).ValidLatitude();
        RuleFor(x => x.Longitude).ValidLongitude();
    }
}

public sealed class SaveCustomerContactRequestValidator : AbstractValidator<SaveCustomerContactRequest>
{
    public SaveCustomerContactRequestValidator()
    {
        RuleFor(x => x.ContactName).ValidName();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Designation).SafeText(100);
    }
}

public sealed class UpdateOwnerRequestValidator : AbstractValidator<UpdateOwnerRequest>
{
    public UpdateOwnerRequestValidator()
    {
        RuleFor(x => x.OwnerType).IsInEnum();
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.PanNumber).Must(IndianFormats.IsValidPan).WithMessage("Enter a valid 10-character PAN.")
            .When(x => !string.IsNullOrWhiteSpace(x.PanNumber));
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class SaveOwnerBusinessRequestValidator : AbstractValidator<SaveOwnerBusinessRequest>
{
    public SaveOwnerBusinessRequestValidator()
    {
        RuleFor(x => x.BusinessName).ValidName(200);
        RuleFor(x => x.GstNumber).ValidOptionalGst();
        RuleFor(x => x.RegistrationNumber).SafeText(50);
        RuleFor(x => x.FleetSize).InclusiveBetween(0, 100000).When(x => x.FleetSize.HasValue);
    }
}

public sealed class SaveOwnerAddressRequestValidator : AbstractValidator<SaveOwnerAddressRequest>
{
    public SaveOwnerAddressRequestValidator()
    {
        RuleFor(x => x.AddressLine1).NotEmpty().SafeText(200);
        RuleFor(x => x.AddressLine2).SafeText(200);
        RuleFor(x => x.Landmark).SafeText(150);
        RuleFor(x => x.CityId).GreaterThan(0);
        RuleFor(x => x.Pincode).ValidPincode();
    }
}

public sealed class CreateBankAccountRequestValidator : AbstractValidator<CreateBankAccountRequest>
{
    public CreateBankAccountRequestValidator()
    {
        RuleFor(x => x.AccountHolderName).ValidName();
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AccountNumber).NotEmpty().Matches("^[0-9]{9,18}$").WithMessage("Account number must be 9-18 digits.");
        RuleFor(x => x.IfscCode).Must(IndianFormats.IsValidIfsc).WithMessage("Enter a valid 11-character IFSC code.");
    }
}

public sealed class VerificationDecisionRequestValidator : AbstractValidator<VerificationDecisionRequest>
{
    public VerificationDecisionRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is VerificationStatus.UnderReview or VerificationStatus.Verified or VerificationStatus.Rejected)
            .WithMessage("Decision must be UnderReview, Verified or Rejected.");
        RuleFor(x => x.Remarks).NotEmpty().When(x => x.Status == VerificationStatus.Rejected)
            .WithMessage("Remarks are required when rejecting.");
        RuleFor(x => x.Remarks).SafeText(500);
    }
}

public sealed class CreateDriverRequestValidator : AbstractValidator<CreateDriverRequest>
{
    public CreateDriverRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.AlternatePhoneNumber).ValidOptionalPhone();
        RuleFor(x => x.DateOfBirth)
            .Must(d => d is null || d.Value <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddYears(-18)))
            .WithMessage("Drivers must be at least 18 years old.");
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9 -]+$");
        RuleFor(x => x.LicenseClass).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LicenseExpiryDate)
            .GreaterThan(_ => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("The driving licence has expired.");
        RuleFor(x => x.IssuingAuthority).SafeText(100);
    }
}

public sealed class UpdateDriverRequestValidator : AbstractValidator<UpdateDriverRequest>
{
    public UpdateDriverRequestValidator()
    {
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.AlternatePhoneNumber).ValidOptionalPhone();
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class SaveDriverLicenseRequestValidator : AbstractValidator<SaveDriverLicenseRequest>
{
    public SaveDriverLicenseRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9 -]+$");
        RuleFor(x => x.LicenseClass).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ExpiryDate).GreaterThan(_ => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .WithMessage("The driving licence has expired.");
        RuleFor(x => x.IssueDate).LessThan(x => x.ExpiryDate).When(x => x.IssueDate.HasValue);
        RuleFor(x => x.IssuingAuthority).SafeText(100);
    }
}

public sealed class SetDriverAvailabilityRequestValidator : AbstractValidator<SetDriverAvailabilityRequest>
{
    public SetDriverAvailabilityRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is DriverAvailabilityStatus.Available or DriverAvailabilityStatus.OffDuty or DriverAvailabilityStatus.Unavailable)
            .WithMessage("Availability can be Available, OffDuty or Unavailable.");
        RuleFor(x => x.Reason).SafeText(300);
    }
}

public sealed class CreateVehicleRequestValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleRequestValidator()
    {
        RuleFor(x => x.VehicleNumber).Must(IndianFormats.IsValidVehicleNumber)
            .WithMessage("Enter a valid registration number, e.g. KA25AB1234 or 22BH1234AA.");
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.Manufacturer).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ManufactureYear).InclusiveBetween(1980, 2100).When(x => x.ManufactureYear.HasValue);
        RuleFor(x => x.CapacityKg).GreaterThan(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.PermitNumber).MaximumLength(50);
        RuleFor(x => x.InsuranceNumber).MaximumLength(50);
    }
}

public sealed class UpdateVehicleRequestValidator : AbstractValidator<UpdateVehicleRequest>
{
    public UpdateVehicleRequestValidator()
    {
        RuleFor(x => x.VehicleTypeId).GreaterThan(0);
        RuleFor(x => x.Manufacturer).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ManufactureYear).InclusiveBetween(1980, 2100).When(x => x.ManufactureYear.HasValue);
        RuleFor(x => x.CapacityKg).GreaterThan(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.PermitNumber).MaximumLength(50);
        RuleFor(x => x.InsuranceNumber).MaximumLength(50);
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.DocumentTypeId).GreaterThan(0);
        RuleFor(x => x.DocumentNumber).MaximumLength(50).Matches("^[A-Za-z0-9 /-]*$").When(x => x.DocumentNumber is not null);
    }
}
