using FluentValidation;

namespace Accounting.Application.Commands;

/// <summary>
/// Validator for CreateAccountCommand
/// </summary>
public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Account name is required")
            .Length(1, 200)
            .WithMessage("Account name must be between 1 and 200 characters");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Invalid account type");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Invalid account status");
    }
}
