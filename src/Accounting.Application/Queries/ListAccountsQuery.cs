using Accounting.Domain.Enums;

namespace Accounting.Application.Queries;

public sealed record ListAccountsQuery(
    AccountStatus? Status = null,
    AccountType? Type = null,
    int Page = 1,
    int PageSize = 20
);

public sealed record ListAccountsResponse(
    List<AccountDto> Accounts,
    PaginationMetadata Pagination
);

public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    AccountStatus Status,
    string Currency,
    DateTime CreatedAt,
    DateTime? ModifiedAt
);

public sealed record PaginationMetadata(
    int CurrentPage,
    int PageSize,
    int TotalPages,
    int TotalCount
);
