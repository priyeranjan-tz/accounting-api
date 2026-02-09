using FluentValidation;

namespace Accounting.Application.Commands;

/// <summary>
/// Validator for GenerateInvoiceCommand
/// </summary>
public class GenerateInvoiceCommandValidator : AbstractValidator<GenerateInvoiceCommand>
{
    public GenerateInvoiceCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Account ID is required");

        RuleFor(x => x.BillingPeriodStart)
            .NotEmpty()
            .WithMessage("Billing period start date is required");

        RuleFor(x => x.BillingPeriodEnd)
            .NotEmpty()
            .WithMessage("Billing period end date is required")
            .GreaterThan(x => x.BillingPeriodStart)
            .WithMessage("Billing period end must be after start date");

        RuleFor(x => x.PaymentTermsDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PaymentTermsDays.HasValue)
            .WithMessage("Payment terms days must be non-negative");
    }
}
