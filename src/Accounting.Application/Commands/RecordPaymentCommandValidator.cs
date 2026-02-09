using FluentValidation;

namespace Accounting.Application.Commands;

/// <summary>
/// Validator for RecordPaymentCommand.
/// Validates input data before processing payments.
/// </summary>
public class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Account ID is required");

        RuleFor(x => x.PaymentReferenceId)
            .NotEmpty()
            .WithMessage("Payment reference ID is required")
            .MaximumLength(100)
            .WithMessage("Payment reference ID must not exceed 100 characters");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Payment amount must be greater than zero")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Payment amount must not exceed $1,000,000");

        RuleFor(x => x.PaymentDate)
            .NotEmpty()
            .WithMessage("Payment date is required")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
            .WithMessage("Payment date cannot be more than 1 day in the future");

        RuleFor(x => x.PaymentMode)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.PaymentMode))
            .WithMessage("Payment mode must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description must not exceed 500 characters");
    }
}
