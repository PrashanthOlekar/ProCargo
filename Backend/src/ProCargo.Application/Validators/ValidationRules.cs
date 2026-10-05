using FluentValidation;
using ProCargo.Domain.ValueObjects;

namespace ProCargo.Application.Validators;

/// <summary>Reusable FluentValidation rules shared by all request validators.</summary>
public static class ValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an upper-case letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lower-case letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(256).EmailAddress().WithMessage("Enter a valid e-mail address.");

    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(20).Must(IndianFormats.IsValidPhone).WithMessage("Enter a valid phone number.");

    public static IRuleBuilderOptions<T, string?> ValidOptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(p => string.IsNullOrWhiteSpace(p) || IndianFormats.IsValidPhone(p)).WithMessage("Enter a valid phone number.");

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule, int max = 150) =>
        rule.NotEmpty().MaximumLength(max).Matches(@"^[\p{L}\p{M}0-9 .,'&()\-/]+$").WithMessage("Name contains invalid characters.");

    public static IRuleBuilderOptions<T, string?> ValidOptionalGst<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(g => string.IsNullOrWhiteSpace(g) || IndianFormats.IsValidGst(g)).WithMessage("Enter a valid 15-character GSTIN.");

    public static IRuleBuilderOptions<T, string> ValidPincode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(IndianFormats.IsValidPincode).WithMessage("Enter a valid 6-digit PIN code.");

    public static IRuleBuilderOptions<T, byte[]> ValidRowVersion<T>(this IRuleBuilder<T, byte[]> rule) =>
        rule.NotNull().Must(r => r.Length == 8).WithMessage("rowVersion is required for updates (reload the record).");

    public static IRuleBuilderOptions<T, decimal?> ValidLatitude<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, decimal?> ValidLongitude<T>(this IRuleBuilder<T, decimal?> rule) =>
        rule.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptions<T, string?> SafeText<T>(this IRuleBuilder<T, string?> rule, int max) =>
        rule.MaximumLength(max).Must(t => t is null || !t.Contains('<', StringComparison.Ordinal))
            .WithMessage("HTML is not allowed.");
}
