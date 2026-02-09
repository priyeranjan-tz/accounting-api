using Accounting.Domain.Enums;

namespace Accounting.Application.Commands;

public sealed record CreateAccountCommand(
    string Name,
    AccountType Type,
    AccountStatus Status,
    InvoiceFrequency InvoiceFrequency = InvoiceFrequency.Monthly
);

public sealed record CreateAccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    AccountStatus Status,
    string Currency,
    DateTime CreatedAt
);
