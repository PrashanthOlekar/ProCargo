using FluentValidation;
using ProCargo.Application.Requests;

namespace ProCargo.Application.Validators;

public sealed class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequest>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0).When(x => x.BookingId.HasValue);
        RuleFor(x => x.Subject).NotEmpty().SafeText(200);
        RuleFor(x => x.Description).NotEmpty().SafeText(2000);
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class UpdateSupportTicketRequestValidator : AbstractValidator<UpdateSupportTicketRequest>
{
    public UpdateSupportTicketRequestValidator()
    {
        RuleFor(x => x.AssignedToUserId).GreaterThan(0).When(x => x.AssignedToUserId.HasValue);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class AddTicketCommentRequestValidator : AbstractValidator<AddTicketCommentRequest>
{
    public AddTicketCommentRequestValidator()
    {
        RuleFor(x => x.CommentText).NotEmpty().SafeText(2000);
    }
}

public sealed class CreateComplaintRequestValidator : AbstractValidator<CreateComplaintRequest>
{
    private static readonly string[] Categories = ["Delay", "Damage", "Behaviour", "Billing", "Payment", "Other"];

    public CreateComplaintRequestValidator()
    {
        RuleFor(x => x.Category).NotEmpty().Must(c => Categories.Contains(c))
            .WithMessage($"Category must be one of: {string.Join(", ", Categories)}.");
        RuleFor(x => x.Subject).NotEmpty().SafeText(200);
        RuleFor(x => x.Description).NotEmpty().SafeText(2000);
        RuleFor(x => x.BookingId).GreaterThan(0).When(x => x.BookingId.HasValue);
        RuleFor(x => x.TripId).GreaterThan(0).When(x => x.TripId.HasValue);
    }
}

public sealed class AssignComplaintRequestValidator : AbstractValidator<AssignComplaintRequest>
{
    public AssignComplaintRequestValidator()
    {
        RuleFor(x => x.AssignedToUserId).GreaterThan(0);
    }
}

public sealed class ChangeComplaintStatusRequestValidator : AbstractValidator<ChangeComplaintStatusRequest>
{
    public ChangeComplaintStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Resolution).NotEmpty()
            .When(x => x.Status is Domain.Enums.ComplaintStatus.Resolved or Domain.Enums.ComplaintStatus.Rejected)
            .WithMessage("Describe the resolution.");
        RuleFor(x => x.Resolution).SafeText(2000);
    }
}

public sealed class ContactEnquiryRequestValidator : AbstractValidator<ContactEnquiryRequest>
{
    public ContactEnquiryRequestValidator()
    {
        RuleFor(x => x.FullName).ValidName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).ValidOptionalPhone();
        RuleFor(x => x.Subject).NotEmpty().SafeText(200);
        RuleFor(x => x.Message).NotEmpty().SafeText(2000);
    }
}

public sealed class UpdateNotificationTemplateRequestValidator : AbstractValidator<UpdateNotificationTemplateRequest>
{
    public UpdateNotificationTemplateRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000)
            .Must(b => !b.Contains("<script", StringComparison.OrdinalIgnoreCase) && !b.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Templates cannot contain scripts.");
    }
}

public sealed class ReportPeriodRequestValidator : AbstractValidator<ReportPeriodRequest>
{
    public ReportPeriodRequestValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From);
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 731).WithMessage("The period can be at most two years.");
        RuleFor(x => x.GroupBy).Must(g => g is "Day" or "Month").WithMessage("GroupBy must be Day or Month.");
        RuleFor(x => x.Top).InclusiveBetween(1, 100);
    }
}
