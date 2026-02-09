using Accounting.Domain.Enums;

namespace Accounting.Application.Queries;

public sealed record GetAccountQuery(Guid AccountId);

public sealed record GetAccountResponse(
    Guid Id,
    string Name,
    AccountType Type,
    AccountStatus Status,
    string Currency,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy
);
