namespace Accounting.Domain.ValueObjects;

/// <summary>
/// Strongly-typed invoice number with tenant-scoped uniqueness.
/// Human-readable invoice numbers prevent collisions across tenants.
/// </summary>
public readonly record struct InvoiceNumber
{
    /// <summary>
    /// The formatted invoice number (e.g., "INV-2026-001").
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Creates a new InvoiceNumber with the specified value.
    /// </summary>
    /// <param name="value">The formatted invoice number.</param>
    public InvoiceNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Invoice number cannot be null or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Creates an InvoiceNumber from a string value.
    /// </summary>
    /// <param name="value">The invoice number string.</param>
    /// <returns>A new InvoiceNumber instance.</returns>
    public static InvoiceNumber FromString(string value) => new(value);

    /// <summary>
    /// Generates a new invoice number using the format INV-{year}-{sequence}.
    /// </summary>
    /// <param name="year">The year for the invoice.</param>
    /// <param name="sequence">The sequence number within the year.</param>
    /// <returns>A new InvoiceNumber instance.</returns>
    public static InvoiceNumber Generate(int year, int sequence)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentException("Year must be between 2000 and 2100.", nameof(year));
        }

        if (sequence < 1)
        {
            throw new ArgumentException("Sequence must be greater than zero.", nameof(sequence));
        }

        var formatted = $"INV-{year}-{sequence:D3}";
        return new InvoiceNumber(formatted);
    }

    /// <summary>
    /// Returns the string representation of this InvoiceNumber.
    /// </summary>
    /// <returns>The invoice number as a string.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts an InvoiceNumber to a string.
    /// </summary>
    public static implicit operator string(InvoiceNumber invoiceNumber) => invoiceNumber.Value;

    /// <summary>
    /// Explicitly converts a string to an InvoiceNumber.
    /// </summary>
    public static explicit operator InvoiceNumber(string value) => new(value);
}
