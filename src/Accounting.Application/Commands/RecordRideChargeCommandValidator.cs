using FluentValidation;

namespace Accounting.Application.Commands;

/// <summary>
/// Validator for RecordRideChargeCommand.
/// Validates input data before processing ride charges.
/// </summary>
public class RecordRideChargeCommandValidator : AbstractValidator<RecordRideChargeCommand>
{
    public RecordRideChargeCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Account ID is required");

        RuleFor(x => x.RideId)
            .NotEmpty()
            .WithMessage("Ride ID is required")
            .MaximumLength(100)
            .WithMessage("Ride ID must not exceed 100 characters");

        RuleFor(x => x.FareAmount)
            .GreaterThan(0)
            .WithMessage("Fare amount must be greater than zero")
            .LessThanOrEqualTo(1000000)
            .WithMessage("Fare amount must not exceed $1,000,000");

        RuleFor(x => x.ServiceDate)
            .NotEmpty()
            .WithMessage("Service date is required")
            .LessThanOrEqualTo(DateTime.UtcNow.AddDays(1))
            .WithMessage("Service date cannot be more than 1 day in the future");

        RuleFor(x => x.FleetId)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.FleetId))
            .WithMessage("Fleet ID must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description must not exceed 500 characters");
    }
}
