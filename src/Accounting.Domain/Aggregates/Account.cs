using Accounting.Domain.Enums;
using Accounting.Domain.ValueObjects;

namespace Accounting.Domain.Aggregates;

/// <summary>
/// Account aggregate root representing a financially responsible entity (organization or individual)
/// </summary>
public sealed class Account
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public AccountType Type { get; private set; }
    public AccountStatus Status { get; private set; }
    public InvoiceFrequency InvoiceFrequency { get; private set; }
    public Guid TenantId { get; private set; }
    public string Currency { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Account() { } // Required for EF Core

    private Account(
        Guid id,
        string name,
        AccountType type,
        AccountStatus status,
        InvoiceFrequency invoiceFrequency,
        Guid tenantId,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name cannot be empty", nameof(name));

        if (name.Length > 200 || name.Length < 1)
            throw new ArgumentException("Account name must be between 1 and 200 characters", nameof(name));

        Id = id;
        Name = name.Trim();
        Type = type;
        Status = status;
        InvoiceFrequency = invoiceFrequency;
        TenantId = tenantId;
        Currency = "USD"; // Fixed to USD per requirements
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
    }

    /// <summary>
    /// Creates a new active account
    /// </summary>
    public static Account Create(
        Guid id,
        string name,
        AccountType type,
        Guid tenantId,
        string createdBy,
        InvoiceFrequency invoiceFrequency = InvoiceFrequency.Monthly)
    {
        return new Account(id, name, type, AccountStatus.Active, invoiceFrequency, tenantId, createdBy);
    }

    /// <summary>
    /// Activates the account allowing it to receive transactions
    /// </summary>
    public void Activate(string modifiedBy)
    {
        if (Status == AccountStatus.Active)
            return; // Already active, idempotent

        Status = AccountStatus.Active;
        ModifiedAt = DateTime.UtcNow;
        ModifiedBy = modifiedBy ?? throw new ArgumentNullException(nameof(modifiedBy));
    }

    /// <summary>
    /// Deactivates the account preventing new transactions
    /// </summary>
    public void Deactivate(string modifiedBy)
    {
        if (Status == AccountStatus.Inactive)
            return; // Already inactive, idempotent

        Status = AccountStatus.Inactive;
        ModifiedAt = DateTime.UtcNow;
        ModifiedBy = modifiedBy ?? throw new ArgumentNullException(nameof(modifiedBy));
    }

    /// <summary>
    /// Determines if the account can receive new transactions
    /// </summary>
    public bool CanReceiveTransactions() => Status == AccountStatus.Active;
}
