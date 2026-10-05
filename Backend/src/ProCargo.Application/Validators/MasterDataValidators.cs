using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class SaveStateRequestValidator : AbstractValidator<SaveStateRequest>
{
    public SaveStateRequestValidator()
    {
        RuleFor(x => x.StateCode).NotEmpty().Length(2).Matches("^[A-Za-z]{2}$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class SaveCityRequestValidator : AbstractValidator<SaveCityRequest>
{
    public SaveCityRequestValidator()
    {
        RuleFor(x => x.StateId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class SaveVehicleTypeRequestValidator : AbstractValidator<SaveVehicleTypeRequest>
{
    public SaveVehicleTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).SafeText(500);
        RuleFor(x => x.CapacityKg).GreaterThan(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.LengthFt).GreaterThan(0).LessThan(200).When(x => x.LengthFt.HasValue);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class SaveGoodsTypeRequestValidator : AbstractValidator<SaveGoodsTypeRequest>
{
    public SaveGoodsTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class SaveDocumentTypeRequestValidator : AbstractValidator<SaveDocumentTypeRequest>
{
    private static readonly string[] Targets = ["Customer", "Owner", "Driver", "Vehicle"];

    public SaveDocumentTypeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9_]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AppliesTo).Must(Targets.Contains).WithMessage("AppliesTo must be Customer, Owner, Driver or Vehicle.");
    }
}

public sealed class UpdateSystemSettingRequestValidator : AbstractValidator<UpdateSystemSettingRequest>
{
    public UpdateSystemSettingRequestValidator()
    {
        RuleFor(x => x.Value).NotNull().MaximumLength(1000);
    }
}
