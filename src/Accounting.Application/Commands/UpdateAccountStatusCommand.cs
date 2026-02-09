using Accounting.Domain.Enums;

namespace Accounting.Application.Commands;

public sealed record UpdateAccountStatusCommand(
    Guid AccountId,
    AccountStatus Status
);

public sealed record UpdateAccountStatusResponse(
    Guid Id,
    string Name,
    AccountType Type,
    AccountStatus Status,
    DateTime? ModifiedAt
);
