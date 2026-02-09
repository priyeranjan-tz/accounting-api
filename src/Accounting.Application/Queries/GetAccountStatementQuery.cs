namespace Accounting.Application.Queries;

/// <summary>
/// Query to retrieve an account statement for a specific date range
/// </summary>
public sealed record GetAccountStatementQuery(
    Guid AccountId,
    DateTime StartDate,
    DateTime EndDate,
    int Page = 1,
    int PageSize = 50
);

/// <summary>
/// Response containing account statement with opening/closing balance and transaction list
/// </summary>
public sealed record GetAccountStatementResponse(
    Guid AccountId,
    string AccountName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    List<StatementTransactionDto> Transactions,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>
/// Individual transaction in an account statement
/// </summary>
public sealed record StatementTransactionDto(
    Guid Id,
    DateTime TransactionDate,
    string LedgerAccount,
    decimal DebitAmount,
    decimal CreditAmount,
    string Description,
    string SourceType,
    Guid? SourceReferenceId
);
