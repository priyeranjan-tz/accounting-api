using Accounting.Domain.Enums;

namespace Accounting.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistence entity for Account (EF Core)
/// </summary>
public class AccountEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public AccountStatus Status { get; set; }
    public InvoiceFrequency InvoiceFrequency { get; set; }
    public Guid TenantId { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}
