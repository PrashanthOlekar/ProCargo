using FluentValidation;
using ProCargo.Application.Requests;
using ProCargo.Domain.Enums;

namespace ProCargo.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.AccountType).IsInEnum();
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.CustomerType).IsInEnum().When(x => x.CustomerType.HasValue);
        RuleFor(x => x.OwnerType).IsInEnum().When(x => x.OwnerType.HasValue);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200)
            .When(x => x.AccountType == AccountType.Customer && x.CustomerType == CustomerType.Business)
            .WithMessage("Company name is required for business customers.");
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200)
            .When(x => x.AccountType == AccountType.VehicleOwner && x.OwnerType == OwnerType.FleetBusiness)
            .WithMessage("Business name is required for fleet businesses.");
        RuleFor(x => x.GstNumber).ValidOptionalGst();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Portal).IsInEnum();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Portal).IsInEnum();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).ValidPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different from the current one.");
    }
}

public sealed class CreateInternalUserRequestValidator : AbstractValidator<CreateInternalUserRequest>
{
    public CreateInternalUserRequestValidator()
    {
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("Select at least one role.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.RowVersion).ValidRowVersion();
    }
}

public sealed class SetUserRolesRequestValidator : AbstractValidator<SetUserRolesRequest>
{
    public SetUserRolesRequestValidator()
    {
        RuleFor(x => x.RoleIds).NotEmpty();
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).Matches("^[A-Za-z][A-Za-z0-9]+$")
            .WithMessage("Role names are a single word of letters and digits.");
        RuleFor(x => x.Description).SafeText(200);
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Description).SafeText(200);
    }
}
