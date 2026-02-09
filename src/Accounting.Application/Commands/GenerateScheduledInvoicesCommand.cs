using Accounting.Domain.Enums;

namespace Accounting.Application.Commands;

/// <summary>
/// Command to generate invoices for accounts based on their invoice frequency
/// </summary>
public sealed record GenerateScheduledInvoicesCommand(
    InvoiceFrequency Frequency,
    DateTime GenerationDate
);

/// <summary>
/// Response for scheduled invoice generation
/// </summary>
public sealed record GenerateScheduledInvoicesResponse(
    int AccountsProcessed,
    int InvoicesGenerated,
    int FailedAccounts,
    DateTime GenerationDate
);
