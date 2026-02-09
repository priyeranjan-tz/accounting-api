using Accounting.Domain.Aggregates;
using Accounting.Domain.Enums;

namespace Accounting.Domain.Interfaces;

/// <summary>
/// Repository interface for Account aggregate
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Creates a new account
    /// </summary>
    Task<Account> CreateAsync(Account account, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an account by ID with tenant filtering
    /// </summary>
    Task<Account?> GetByIdAsync(Guid accountId, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists accounts with filtering and pagination
    /// </summary>
    Task<(List<Account> Accounts,int TotalCount)> ListAsync(
        Guid tenantId,
        AccountStatus? status,
        AccountType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks if an account with the given name exists for the tenant
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all active accounts with the specified invoice frequency
    /// </summary>
    Task<IEnumerable<Account>> GetByInvoiceFrequencyAsync(
        InvoiceFrequency frequency, 
        Guid tenantId, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing account
    /// </summary>
    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
