namespace Accounting.Domain.Enums;

/// <summary>
/// Frequency at which invoices are automatically generated for an account.
/// </summary>
public enum InvoiceFrequency
{
    /// <summary>
    /// Generate an invoice immediately after each ride (real-time invoicing).
    /// </summary>
    PerRide = 0,

    /// <summary>
    /// Generate invoices daily at midnight UTC.
    /// </summary>
    Daily = 1,

    /// <summary>
    /// Generate invoices weekly on Sunday at midnight UTC.
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// Generate invoices monthly on the last day of the month at midnight UTC.
    /// </summary>
    Monthly = 3
}
